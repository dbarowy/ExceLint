﻿namespace ExceLint
    open System
    open System.Collections.Generic
    open System.Collections.Immutable
    open Utils
    open CommonTypes
    open HashSetUtils
    open FastDependenceAnalysis

    module CommonFunctions =
            // _analysis_base specifies which cells should be ranked:
            // 1. allCells means all cells in the spreadsheet
            // 2. onlyFormulas means only formulas
            let analysisBase(config: FeatureConf)(d: Graph) : AST.Address[] =
                let cs = if config.IsEnabled("AnalyzeOnlyFormulas") then
                            d.getAllFormulaAddrs()
                         else
//                            d.allCells()
                            d.allCellsIncludingBlanks()
                let cs' = match config.IsLimitedToSheet with
                          | Some(wsname) -> cs |> Array.filter (fun addr -> addr.A1Worksheet() = wsname)
                          | None -> cs 
                cs'

            let runEnabledFeatures(cells: AST.Address[])(dag: Graph)(config: FeatureConf)(progress: Progress) =
                config.EnabledFeatures |>
                Array.map (fun fname ->
                    // get feature lambda
                    let feature = config.FeatureByName fname

                    // choose the appropriate mapper
                    let mapif = if cells.Length > 100 then Array.Parallel.mapi else Array.mapi

                    let fvals =
                        mapif (fun i cell ->
                            if progress.IsCancelled() then
                                raise AnalysisCancelled

                            if i % 10 = 0 then
                                progress.IncrementCounterN(10)
                            cell, feature cell dag
                        ) cells
                    
                    fname, fvals
                ) |> adict

            let invertedHistogram(scoretable: ScoreTable)(dag: Graph)(config: FeatureConf) : ROInvertedHistogram =
                assert (config.EnabledScopes.Length = 1 && config.EnabledFeatures.Length = 1)

                let d = ImmutableDictionary.CreateBuilder<AST.Address, HistoBin>()

                Array.iter (fun fname ->
                    Array.iter (fun (sel: Scope.Selector) ->
                        Array.iter (fun (addr: AST.Address, score: Countable) ->
                            // fetch SelectID for this selector and address
                            let sID = sel.id addr dag

                            // get binname
                            let binname = fname,sID,score

                            d.Add(addr,binname)
                        ) (scoretable.[fname])
                    ) (config.EnabledScopes)
                ) (config.EnabledFeatures)

                d.ToImmutable()

            let centroid(c: seq<AST.Address>)(ih: InvertedHistogram) : Countable =
                c
                |> Seq.map (fun a ->
                    let (_,_,c) = ih.[a]    // get histobin for address
                    c                       // get countable from bin
                   )
                |> Countable.Mean               // get mean

            let centroid_ro(c: seq<AST.Address>)(ih: ROInvertedHistogram) : Countable =
                c
                |> Seq.map (fun a ->
                    let (_,_,c) = ih.[a]    // get histobin for address
                    c                       // get countable from bin
                   )
                |> Countable.Mean               // get mean

            let cartesianProductByX(xset: Set<'a>)(yset: Set<'a>) : ('a*'a[]) list =
                // cartesian product, grouped by the first element,
                // excluding the element itself
                Set.map (fun x -> x, (Set.difference yset (Set.ofList [x])) |> Set.toArray) xset |> Set.toList

            let cartesianProduct(xs: seq<'a>)(ys: seq<'b>) : seq<'a*'b> =
                xs |> Seq.collect (fun x -> ys |> Seq.map (fun y -> x, y))

            let ToCountable(a: AST.Address)(ih: InvertedHistogram) : Countable =
                let (_,_,v) = ih.[a]
                v

            let nop = Progress.NOPProgress()

            let toDict(arr: ('a*'b)[]) : Dict<'a,'b> =
                // assumes that 'a is unique
                let d = new Dict<'a,'b>(arr.Length)
                Array.iter (fun (a,b) ->
                    d.Add(a,b)
                ) arr
                d

            let transitiveInputs(faddr: AST.Address)(dag : Graph) : AST.Address[] =
                let rec tf(addr: AST.Address) : AST.Address list =
                    if (dag.isFormula addr) then
                        // find all of the inputs (local dependencies) for formula
                        let refs = dag.getFormulaSingleCellInputs addr |> List.ofSeq
                        // prepend addr & recursively call this function
                        addr :: (List.map tf refs |> List.concat)
                    else
                        [addr]
    
                tf faddr |> List.toArray

            let noWeights(analysis_base: Graph -> AST.Address[])(dag: Graph) : Weights =
                // get the set of cells to be analyzed
                let cells = analysis_base(dag)

                // for each cell, compute cumulative reference count. the insight here
                // is that summary rows are counting things that are counted by
                // subcomputations; thus, we should inflate their ranks by how much
                // they over-count.
                // this really only makes sense for formulas, but in case the user
                // asked for a ranking of all cells, we compute refcounts here even
                // for non-formulas
                let weights = Array.map (fun f ->
                                  f,1.0
                              ) cells

                weights |> dict

            let weights(input: Input)(analysis: Analysis) : AnalysisOutcome =
                // compute weights
                let weights = noWeights (analysisBase input.config) input.dag

                match analysis with
                | Histogram h -> Success(Histogram({ h with weights = weights }))
                | COF c -> Success(COF({ c with weights = weights }))
                | Cluster c -> Success(Cluster({ c with weights = weights }))

            // sanity check: are scores monotonically increasing?
            let monotonicallyIncreasing(r: Ranking) : bool =
                let mutable last = 0.0
                let mutable outcome = true
                for kvp in r do
                    if kvp.Value >= last then
                        last <- kvp.Value
                    else
                        outcome <- false
                outcome

            // "AngleMin" algorithm
            let dderiv(y: double[]) : int =
                let mutable anglemin = 1.0
                let mutable angleminindex = 0
                for index in 0..(y.Length - 3) do
                    let angle = y.[index] + y.[index + 2] - 2.0 * y.[index + 1]
                    if angle < anglemin then
                        anglemin <- angle
                        angleminindex <- index
                angleminindex

            let equivalenceClasses(ranking: Ranking) : Dict<AST.Address,int> =
                let rankgrps = Array.groupBy (fun (kvp: KeyValuePair<AST.Address,double>) -> kvp.Value) ranking

                let grpids = Array.mapi (fun i (hb,_) -> hb,i) rankgrps |> adict

                let output = Array.map (fun (kvp: KeyValuePair<AST.Address,double>) -> kvp.Key, grpids.[kvp.Value]) ranking |> adict

                output

            let seekEquivalenceBoundary(ranking: Ranking)(cut_idx: int) : int =
                // if we have no anomalies, because either the cut index excludes
                // all cells or because the ranking is zero-length, just return -1
                if cut_idx = -1 || ranking.Length = 0 then
                    -1
                else
                    // a map from AST.Address to equivalence class number
                    let ecs = equivalenceClasses ranking

                    // get the equivalence class of the element at the cut index (inclusive)
                    let cutEC = ecs.[ranking.[cut_idx].Key]

                    // the last index in the ranking is
                    let lastidx = ranking.Length - 1

                    // if there is a "next element" in the ranking after the cut,
                    // is it in the same equivalence class?
                    if lastidx >= cut_idx + 1 && cutEC = ecs.[ranking.[cut_idx + 1].Key] then
                        // find the first index that is different by scanning backward
                        if cut_idx <= 0 then
                            -1
                        else
                            // find the index in the ranking where the equivalence class changes
                            let mutable seek_idx = cut_idx - 1
                            while seek_idx >= 0 && ecs.[ranking.[seek_idx].Key] = cutEC do
                                seek_idx <- seek_idx - 1
                            seek_idx
                    else
                        cut_idx

            // returns the index of the last element to KEEP
            // returns -1 if you should keep nothing
            let private findCutIndex(ranking: Ranking)(thresh_idx: int): int =
                if ranking.Length = 0 then
                    -1
                else
                    // extract totally-ordered score vector
                    let rank_nums = Array.map (fun (kvp: KeyValuePair<AST.Address,double>) -> kvp.Value) ranking

                    // cut it at thresh (inclusive)
                    let rank_nums' = rank_nums.[..thresh_idx]

                    // find the index of the "knee"
                    dderiv(rank_nums')

            let kneeIndexOpt(input: Input)(analysis: Analysis) : AnalysisOutcome =
                let ranking = match analysis with 
                              | Histogram h -> h.ranking 
                              | COF c -> c.ranking 
                              | Cluster c -> c.ranking

                let sig_threshold_idx =
                    match analysis with 
                    | Histogram h -> h.sig_threshold_idx 
                    | COF c -> c.sig_threshold_idx 
                    | Cluster c -> c.sig_threshold_idx

                let idx =
                    if input.config.IsEnabledSpectralRanking then
                        // compute knee cutoff
                        findCutIndex ranking sig_threshold_idx
                    else
                        // stick with total %
                        sig_threshold_idx
                // does the cut index straddle an equivalence class?
                let ce = seekEquivalenceBoundary ranking idx

                match analysis with
                | Histogram h -> Success(Histogram({ h with cutoff_idx = ce }))
                | COF c -> Success(COF({ c with cutoff_idx = ce }))
                | Cluster c -> Success(Cluster({ c with cutoff_idx = ce }))

            let cutoffIndex(input: Input)(analysis: Analysis) : AnalysisOutcome =
                let ranking = match analysis with | Histogram h -> h.ranking | COF c -> c.ranking | Cluster c -> c.ranking

                // compute total mass of distribution
                let total_mass = double ranking.Length
                // compute the index of the maximum element
                let idx = int (Math.Floor(total_mass * input.alpha))

                match analysis with
                | Histogram h -> Success(Histogram({ h with sig_threshold_idx = idx }))
                | COF c -> Success(COF({ c with sig_threshold_idx = idx }))
                | Cluster c -> Success(Cluster({ c with sig_threshold_idx = idx }))

            let canonicalSort(input: Input)(analysis: Analysis) : AnalysisOutcome =
                let r = match analysis with | Histogram h -> h.ranking | COF c -> c.ranking | Cluster c -> c.ranking
                let arr = Array.sortWith (fun (a: KeyValuePair<AST.Address,double>)(b: KeyValuePair<AST.Address,double>) ->
                                              let a_addr: AST.Address = a.Key
                                              let a_score: double = a.Value
                                              let b_addr: AST.Address = b.Key
                                              let b_score: double = b.Value

                                              if a_score < b_score then
                                                  -1
                                              elif a_score = b_score then
                                                  if a_addr.Col < b_addr.Col then
                                                      -1
                                                  elif a_addr.Col = b_addr.Col then
                                                      if a_addr.Row < b_addr.Row then
                                                          -1
                                                      elif a_addr.Row = b_addr.Row then
                                                          0
                                                      else
                                                          1
                                                  else
                                                      1
                                              else
                                                  1
                                         ) r
                assert (monotonicallyIncreasing arr)

                match analysis with
                | Histogram h -> Success(Histogram({ h with ranking = arr }))
                | COF c -> Success(COF({ c with ranking = Array.rev arr }))
                | Cluster c -> Success(Cluster({ c with ranking = Array.rev arr }))

            let reweightRanking(input: Input)(analysis: Analysis) : AnalysisOutcome =
                let ranking = match analysis with | Histogram h -> h.ranking | COF c -> c.ranking | Cluster c -> c.ranking
                let weights = match analysis with | Histogram h -> h.weights | COF c -> c.weights | Cluster c -> c.weights

                let ranking' = 
                    ranking
                    |> Array.map (fun (kvp: KeyValuePair<AST.Address,double>) ->
                        let addr = kvp.Key
                        let score = kvp.Value
                        new KeyValuePair<AST.Address,double>(addr, weights.[addr] * score)
                      )

                match analysis with
                | Histogram h -> Success(Histogram({ h with ranking = ranking' }))
                | COF c -> Success(COF({ c with ranking = ranking' }))
                | Cluster c -> Success(Cluster({ c with ranking = ranking' }))

            // find a vector guaranteed to be longer than any of the given vectors
            let diagonalScaleFactor(ss: ScoreTable) : double =
                let points = ss
                                |> Seq.map (fun kvp -> kvp.Value |> Seq.map (fun (_,c: Countable) -> c.Location))
                                |> Seq.concat
                                |> Seq.toArray
                let min_init = points.[0]
                let max_init = points.[0]
                let minf = (fun (a: double)(b: double) -> if a < b then a else b )
                let maxf = (fun (a: double)(b: double) -> if a > b then a else b )
                let min = points |> Seq.fold (fun (minc:Countable)(c:Countable) -> minc.ElementwiseOp c minf) min_init
                let max = points |> Seq.fold (fun (maxc:Countable)(c:Countable) -> maxc.ElementwiseOp c maxf) max_init
                min.EuclideanDistance max

            /// <summary>
            /// Scales the countables in the ScoreTable by a per-
            /// sheet diagonal scale factor.
            /// </summary>
            /// <param name="s"></param>
            let ScaleBySheet(s: ScoreTable) : ScoreTable =
                // scale each sheet separately
                s
                |> Seq.map (fun kvp ->
                        let feature = kvp.Key
                        let dist = kvp.Value
                        let scaledDist =
                            dist
                            |> Seq.groupBy (fun (addr,co) ->
                                    addr.WorksheetName
                                )
                            |> Seq.map (fun (wsname,cells) ->
                                    // this is conditioned on sheet name, so
                                    // turn into a ScoreTable again
                                    let acells = cells |> Seq.toArray
                                    let stForSheet: ScoreTable = [(feature,acells)] |> adict
                                    // compute scale factor
                                    let factor = diagonalScaleFactor stForSheet
                                    // scale each vector unless it is off-sheet
                                    cells |>
                                    Seq.map (fun (addr,co) ->
                                        if co.IsOffSheet then
                                            // don't scale
                                            addr,co
                                        else
                                            // scale
                                            addr,co.UpdateResultant (co.ToCVectorResultant.ScalarMultiply factor)
                                    )
                                
                                )
                            |> Seq.concat
                            |> Seq.toArray
                        feature, scaledDist
                    )
                |> adict

            let ReverseClusterLookup(clusters: ImmutableClustering) : ImmutableDictionary<AST.Address,ImmutableHashSet<AST.Address>> =
                let builder = ImmutableDictionary.CreateBuilder<AST.Address, ImmutableHashSet<AST.Address>>()
                let revLookup = new Dict<AST.Address,HashSet<AST.Address>>()
                for c in clusters do
                    for a in c do
                        try
                            builder.Add(a,c)
                        with
                        | e ->
                            let other = builder.[a]
                            failwith "badness"
                builder.ToImmutable()

            let ReverseClusterLookupMutable(clusters: Clustering) : Dictionary<AST.Address,HashSet<AST.Address>> =
                let d = new Dict<AST.Address, HashSet<AST.Address>>()
                let revLookup = new Dict<AST.Address,HashSet<AST.Address>>()
                for c in clusters do
                    for a in c do
                        d.Add(a,c)
                d

            let ToMutableClustering<'p>(clustering: ImmutableGenericClustering<'p>) : GenericClustering<'p> =
                let clustering' =
                    Seq.map (fun cl ->
                        new HashSet<'p>(Seq.toArray cl)
                    ) clustering

                new HashSet<HashSet<'p>>(clustering')

            let CopyClustering<'p>(clustering: GenericClustering<'p>) : GenericClustering<'p> =
                let clustering' =
                    Seq.map (fun cl ->
                        new HashSet<'p>(Seq.toArray cl)
                    ) clustering

                new HashSet<HashSet<'p>>(clustering')

            let SameClusters(cs: seq<ImmutableHashSet<AST.Address>>) : bool =
                let first = Seq.head cs
                cs |> Seq.fold (fun acc c -> acc && first.SetEquals c) true 

            let SameClustering(c1: ImmutableClustering)(c2: ImmutableClustering) : bool =
                try 
                    let c1R = ReverseClusterLookup c1
                    let c2R = ReverseClusterLookup c2

                    let mutable ok = true

                    // lookup every address in every cluster in c1
                    // and make sure that:
                    // 1. the cluster in c2 is the same cluster
                    // 2. the cluster in c2 and the cluster in c1 are the same cluster
                    for c in c1 do
                        let c2cs = c |> Seq.map (fun a -> c2R.[a])
                        if not (SameClusters c2cs) then
                            ok <- false
                        if not (c.SetEquals(Seq.head c2cs)) then
                            ok <- false

                    // ditto but for c2
                    for c in c2 do
                        let c1cs = c |> Seq.map (fun a -> c1R.[a])
                        if not (SameClusters c1cs) then
                            ok <- false
                        if not (c.SetEquals(Seq.head c1cs)) then
                            ok <- false

                    ok
                with
                | _ -> false

            let ClusterToString(c: ImmutableHashSet<AST.Address>) : string =
                System.String.Join(", ", c |> Seq.map (fun a -> a.A1Local.ToString()))

            type Diffs =
            | MissingInSecond of AST.Address
            | MissingInFirst of AST.Address
            | ClustersDifferent of ImmutableHashSet<AST.Address>*ImmutableHashSet<AST.Address>
                override self.ToString() =
                    match self with
                    | MissingInSecond(a) -> "The second clustering is missing address: " + a.A1Local().ToString()
                    | MissingInFirst(a) -> "The first clustering is missing address: " + a.A1Local().ToString()
                    | ClustersDifferent(c1,c2) -> "Cluster { " + (ClusterToString c1) + " } different than { " + (ClusterToString c2) + " }"

            let ClusterDiff(c1: ImmutableClustering)(c2: ImmutableClustering) : Diffs[] =
                let c1R = ReverseClusterLookup c1
                let c2R = ReverseClusterLookup c2

                let d = new Dict<string, Diffs>()

                for pair in c1R do
                    let addr = pair.Key
                    let c1c = pair.Value
                    if not (c2R.ContainsKey addr) then
                        let diff = MissingInSecond(addr)
                        let dstr = diff.ToString()
                        if not (d.ContainsKey dstr) then
                            d.Add(dstr, diff)
                    else
                        let c1str = ClusterToString c1c
                        let c2str = ClusterToString c2R.[addr]
                        if c1str <> c2str then
                            let diff = ClustersDifferent(c1c, c2R.[addr])
                            let dstr = diff.ToString()
                            if not (d.ContainsKey dstr) then
                                d.Add(dstr, diff)

                for pair in c2R do
                    let addr = pair.Key
                    let c2c = pair.Value
                    if not (c1R.ContainsKey addr) then
                        let diff = MissingInFirst(addr)
                        let dstr = diff.ToString()
                        if not (d.ContainsKey dstr) then
                            d.Add(dstr, diff)
                    else
                        let c2str = ClusterToString c2c
                        let c1str = ClusterToString c1R.[addr]
                        if c2str <> c1str then
                            let diff = ClustersDifferent(c2c, c1R.[addr])
                            let dstr = diff.ToString()
                            if not (d.ContainsKey dstr) then
                                d.Add(dstr, diff)

                d.Values |> Seq.toArray

            let numberClusters(clustering: Clustering) : ClusterIDs =
                let d = new Dict<HashSet<AST.Address>,int>()
                let mutable i = 0
                Seq.iter (fun c ->
                    if not (d.ContainsKey c) then
                        d.Add(c, i)
                        i <- i + 1
                ) clustering
                d

            let makeFlatScoreTable(scores: ScoreTable) : FlatScoreTable =
                let mutable max = 0
                for arr in scores do
                    if arr.Value.Length > max then
                        max <- arr.Value.Length

                let d = new Dict<string*AST.Address,Countable>(max * scores.Count)
                
                Seq.iter (fun (kvp: KeyValuePair<string,(AST.Address*Countable)[]>) ->
                    let fname = kvp.Key
                    let arr = kvp.Value
                    Array.iter (fun (addr,score) ->
                        d.Add((fname,addr), score)
                    ) arr
                ) scores

                d

            let ROInvertedHistogramToScoreTable(ih: ROInvertedHistogram) : ScoreTable =
                let d = new ScoreTable()
                ih
                |> Seq.map (fun kvp ->
                       let addr = kvp.Key
                       let (feat,scope,countable) = kvp.Value
                       feat, addr, countable
                   )
                |> Seq.groupBy(fun (feat,_,_) -> feat)
                |> Seq.iter (fun (key, xs) ->
                       let xs' =
                           xs
                           |> Seq.map (fun (feat, addr, countable) -> addr, countable)
                           |> Seq.toArray
                       d.Add(key, xs')
                   )
                d