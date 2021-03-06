﻿namespace ExceLint

    open System.Collections.Generic
    open System.Collections.Immutable
    open CommonTypes
    open CommonFunctions
    open Utils

    type Cells = Dict<AST.Address,Countable>
    type Coord = AST.Address * AST.Address
    type LeftTop = Coord
    type RightBottom = Coord
    type Region = LeftTop * RightBottom

    [<AbstractClass>]
    type BinaryMinEntropyTree(lefttop: AST.Address, rightbottom: AST.Address, subtree_kind: SubtreeKind) =
        abstract member Region : string
        default self.Region : string = lefttop.A1Local() + ":" + rightbottom.A1Local()
        abstract member ToGraphViz : int -> int*string
        abstract member Subtree : SubtreeKind
        default self.Subtree = subtree_kind

        static member AddressSetEntropy(addrs: AST.Address[])(rmap: Dict<AST.Address,Countable>) : double =
            if addrs.Length = 0 then
                // the decomposition where one side is the empty set
                // is the worst decomposition, unless it is the only
                // decomposition
                System.Double.PositiveInfinity
            else
                // get values
                let vs = addrs |> Array.map (fun a -> rmap.[a].ToCVectorResultant)

                // count
                let cs = BasicStats.counts vs

                // compute probability vector
                let ps = BasicStats.empiricalProbabilities cs

                // compute entropy
                let entropy = BasicStats.entropy ps

                entropy

        static member GraphViz(t: BinaryMinEntropyTree) : string =
            let (_,graph) = t.ToGraphViz 0
            "graph {" + graph + "}"

        static member Condition(cs: ImmutableClustering)(attribute: AST.Address -> int)(value: int)(noEmptyClusters) : ImmutableClustering =
            cs
            |> Seq.map (fun cluster ->
                cluster
                |> Seq.filter (fun addr ->
                    (attribute addr) = value
                )
                |> (fun cluster2 ->
                    if noEmptyClusters && Seq.length cluster2 = 0 then
                        None
                    else
                        Some((new HashSet<AST.Address>(cluster2)).ToImmutableHashSet())
                   )
            )
            |> Seq.choose id
            |> (fun cs' -> cs'.ToImmutableHashSet())

        static member ColumnEntropy(cs: ImmutableClustering) : double =
            let cells = cs |> Seq.concat |> Seq.toArray
            let (lt,rb) = Utils.BoundingRegion cells 0
            
            let col = (fun (a: AST.Address) -> a.Y )

            let colE =
                [| lt.Y .. rb.Y |]
                |> Array.sumBy (fun y ->
                    let cs' = BinaryMinEntropyTree.Condition cs col y true
                    let entropy = BinaryMinEntropyTree.NormalizedClusteringEntropy cs'
                    entropy
                )
            colE

        static member RowEntropy(cs: ImmutableClustering) : double =
            let cells = cs |> Seq.concat |> Seq.toArray
            let (lt,rb) = Utils.BoundingRegion cells 0
            
            let row = (fun (a: AST.Address) -> a.X )

            let rowE =
                [| lt.X .. rb.X |]
                |> Array.sumBy (fun x ->
                    let cs' = BinaryMinEntropyTree.Condition cs row x true
                    let entropy = BinaryMinEntropyTree.NormalizedClusteringEntropy cs'
                    entropy
                )
            rowE

        static member GridEntropy(cs: ImmutableClustering) : double =
            BinaryMinEntropyTree.RowEntropy cs + BinaryMinEntropyTree.ColumnEntropy cs

        /// <summary>
        /// Measure the normalized entropy of a clustering, where the number of cells
        /// inside clusters is used to determine frequency.
        /// </summary>
        /// <param name="c">A Clustering</param>
        static member NormalizedClusteringEntropy(c: ImmutableClustering) : double =
            // count
            let cs = c |> Seq.map (fun reg -> reg.Count) |> Seq.toArray

            // n
            let n = Array.sum cs

            // compute probability vector
            let ps = BasicStats.empiricalProbabilities cs

            // compute entropy
            let entropy = BasicStats.normalizedEntropy ps n

            entropy

        /// <summary>
        /// Measure the entropy of a clustering, where the number of cells
        /// inside clusters is used to determine frequency.
        /// </summary>
        /// <param name="c">A Clustering</param>
        static member ClusteringEntropy(c: Clustering) : double =
            // count
            let cs = c |> Seq.map (fun reg -> reg.Count) |> Seq.toArray

            // compute probability vector
            let ps = BasicStats.empiricalProbabilities cs

            // compute entropy
            let entropy = BasicStats.entropy ps

            entropy

        /// <summary>
        /// The difference in clustering entropy between cTo and cFrom. A negative number
        /// denotes a decrease in entropy from cFrom to cTo whereas a positive number
        /// denotes an increase in entropy from cFrom to cTo.
        /// </summary>
        /// <param name="cFrom">original clustering</param>
        /// <param name="cTo">new clustering</param>
        static member ClusteringEntropyDiff(cFrom: ImmutableClustering)(cTo: ImmutableClustering) : double =
            let c1e = BinaryMinEntropyTree.NormalizedClusteringEntropy cFrom
            let c2e = BinaryMinEntropyTree.NormalizedClusteringEntropy cTo
            c2e - c1e

        /// <summary>
        /// The difference in grid entropy between cTo and cFrom. A negative number
        /// denotes a decrease in entropy from cFrom to cTo whereas a positive number
        /// denotes an increase in entropy from cFrom to cTo.
        /// </summary>
        /// <param name="cFrom">original clustering</param>
        /// <param name="cTo">new clustering</param>
        static member GridEntropyDiff(cFrom: ImmutableClustering)(cTo: ImmutableClustering) : double =
            let g1e = BinaryMinEntropyTree.GridEntropy cFrom
            let g2e = BinaryMinEntropyTree.GridEntropy cTo
            g2e - g1e

        static member private MinEntropyPartition(rmap: Cells)(vert: bool) : AST.Address[]*AST.Address[] =
            // which axis we use depends on whether we are
            // decomposing verticalls or horizontally
            let indexer = (fun (a: AST.Address) -> if vert then a.X else a.Y)

            // extract addresses
            let addrs = Array.ofSeq rmap.Keys

            // find bounding box
            let (lt,rb) = Utils.BoundingRegion (rmap.Keys) 0

            let parts = Array.map (fun i ->
                               // partition addresses by "less than index of a",
                               // e.g., if vert then "less than x"
                               let (left,right) = addrs |> Array.partition (fun a -> indexer a < i)

                               // if left and right divide any indivisible clusters,
                               // ignore this partitioning
                               let left' = new HashSet<AST.Address>(left)
                               let right' = new HashSet<AST.Address>(right)

                               left, right
                           ) [| indexer lt .. indexer rb + 1 |]

            assert (parts.Length <> 0)

            parts
            |> Utils.argmin (fun (l,r) ->
                // compute entropy
                let entropy_left = BinaryMinEntropyTree.AddressSetEntropy l rmap
                let entropy_right = BinaryMinEntropyTree.AddressSetEntropy r rmap

                // total for left and right
                entropy_left + entropy_right
            )

        static member MakeCells(hb_inv: ROInvertedHistogram) : Cells =
            let addrs = hb_inv.Keys
            let d = new Dict<AST.Address, Countable>()
            for addr in addrs do
                let (_,_,c) = hb_inv.[addr]
                d.Add(addr, c)
            d

        static member Infer(hb_inv: ROInvertedHistogram) : BinaryMinEntropyTree =
            let rmap = BinaryMinEntropyTree.MakeCells hb_inv
            BinaryMinEntropyTree.Decompose rmap

        static member private IsHomogeneous(c: AST.Address[])(rmap: Cells) : bool =
            let rep = rmap.[c.[0]]
            c |> Array.forall (fun a -> rmap.[a] = rep)

        static member private Decompose (initial_rmap: Cells) : BinaryMinEntropyTree =
            let mutable todos = [ (Root, initial_rmap) ]
            let mutable linkUp = []
            let mutable root_opt = None

            // process work list
            while not todos.IsEmpty do
                // grab next item
                let (subtree_kind, rmap) = todos.Head
                todos <- todos.Tail

                // get bounding region
                let (lefttop,rightbottom) = Utils.BoundingRegion rmap.Keys 0

                // base case 1: there's only 1 cell
                if lefttop = rightbottom then
                    let leaf = Leaf(lefttop, rightbottom, subtree_kind, rmap) :> BinaryMinEntropyTree
                    // add leaf to to link-up list
                    linkUp <- leaf :: linkUp

                    // is this leaf the root?
                    match subtree_kind with
                    | Root -> root_opt <- Some leaf
                    | _ -> ()
                else
                    // find the minimum entropy decompositions
                    let (left,right) = BinaryMinEntropyTree.MinEntropyPartition rmap true
                    let (top,bottom) = BinaryMinEntropyTree.MinEntropyPartition rmap false

                    // compute entropies again
                    let e_vert = BinaryMinEntropyTree.AddressSetEntropy left rmap +
                                    BinaryMinEntropyTree.AddressSetEntropy right rmap
                    let e_horz = BinaryMinEntropyTree.AddressSetEntropy top rmap +
                                    BinaryMinEntropyTree.AddressSetEntropy bottom rmap

                    // split vertically or horizontally (favor vert for ties)
                    let (entropy,p1,p2) =
                        if e_vert <= e_horz then
                            e_vert, left, right
                        else
                            e_horz, top, bottom

                    // base case 2: perfect decomposition & right values same as left values
                    if entropy = 0.0 && rmap.[p1.[0]].ToCVectorResultant = rmap.[p2.[0]].ToCVectorResultant
                    then
                        let leaf = Leaf(lefttop, rightbottom, subtree_kind, rmap) :> BinaryMinEntropyTree

                        // is this leaf the root?
                        match subtree_kind with
                        | Root -> root_opt <- Some leaf
                        | _ -> ()

                        // add leaf to to link-up list
                        linkUp <- leaf :: linkUp
                    else
                        let p1_rmap = p1 |> Array.map (fun a -> a,rmap.[a]) |> adict
                        let p2_rmap = p2 |> Array.map (fun a -> a,rmap.[a]) |> adict

                        let node = Inner(lefttop, rightbottom, subtree_kind)

                        // is this node the root?
                        match subtree_kind with
                        | Root -> root_opt <- Some (node :> BinaryMinEntropyTree)
                        | _ -> ()

                        // add next nodes to work list
                        todos <- (LeftOf node, p1_rmap) :: (RightOf node, p2_rmap) :: todos
                        
            // process "link-up" list
            while not linkUp.IsEmpty do
                // grab next item
                let node = linkUp.Head
                linkUp <- linkUp.Tail

                match node.Subtree with
                | LeftOf parent ->
                    // add parent to linkup list
                    linkUp <- (parent :> BinaryMinEntropyTree) :: linkUp

                    // make link
                    parent.AddLeft node
                | RightOf parent ->
                    // add parent to linkup list
                    linkUp <- (parent :> BinaryMinEntropyTree) :: linkUp

                    // make link
                    parent.AddRight node
                | Root -> ()    // do nothing

            match root_opt with
            | Some root -> root
            | None -> failwith "this should never happen"

        /// <summary>return the leaves of the tree, in order of smallest to largest region</summary>
        static member Regions(tree: BinaryMinEntropyTree) : Leaf[] =
            match tree with
            | :? Inner as i -> Array.append (BinaryMinEntropyTree.Regions (i.Left)) (BinaryMinEntropyTree.Regions (i.Right))
            | :? Leaf as l -> [| l |]
            | _ -> failwith "Unknown tree node type."

        static member MergeIndivisibles(ic: ImmutableClustering)(indivisibles: ImmutableClustering) : ImmutableClustering =
            let cs = ToMutableClustering ic

            // get rmap
            let reverseLookup = ReverseClusterLookupMutable cs

            // coalesce indivisibles
            for i in indivisibles do
                // get the biggest cluster
                let biggest = i |> Seq.map (fun a -> reverseLookup.[a]) |> Seq.maxBy (fun c -> c.Count)
                for a in i do
                    if not (biggest.Contains a) then
                        // get the cluster that currently belongs to
                        let c = reverseLookup.[a]

                        // merge all of the cells from c into biggest
                        if not (biggest = c) then
                            HashSetUtils.inPlaceUnion c biggest

                            // remove c from clustering
                            cs.Remove c |> ignore

            // restore immutability
            ToImmutableClustering cs

        static member Clustering(tree: BinaryMinEntropyTree)(ih: ROInvertedHistogram)(indivisibles: ImmutableClustering) : ImmutableClustering =
            // coalesce rectangular regions
            let cs = BinaryMinEntropyTree.RectangularClustering tree ih

            // merge indivisible clusters
            let cs' = BinaryMinEntropyTree.MergeIndivisibles cs indivisibles

            cs'

        static member ClusterIsRectangular(c: HashSet<AST.Address>) : bool =
            let boundingbox = Utils.BoundingBoxHS c 0
            let diff = HashSetUtils.difference boundingbox c
            let isRect = diff.Count = 0
            isRect

        static member ClusteringContainsOnlyRectangles(cs: Clustering) : bool =
            cs |> Seq.fold (fun a c -> a && BinaryMinEntropyTree.ClusterIsRectangular c) true

        static member CellMergeIsRectangular(source: AST.Address)(target: ImmutableHashSet<AST.Address>) : bool =
            let merged = new HashSet<AST.Address>(target.Add source)
            BinaryMinEntropyTree.ClusterIsRectangular merged

        static member ImmMergeIsRectangular(source: ImmutableHashSet<AST.Address>)(target: ImmutableHashSet<AST.Address>) : bool =
            let merged = source.Union target
            BinaryMinEntropyTree.ClusterIsRectangular (new HashSet<AST.Address>(merged))

        static member MergeIsRectangular(source: HashSet<AST.Address>)(target: HashSet<AST.Address>) : bool =
            let merged = HashSetUtils.union source target
            BinaryMinEntropyTree.ClusterIsRectangular merged

        static member CoaleseAdjacentClusters(coal_vert: bool)(clusters: ImmutableClustering)(hb_inv: ROInvertedHistogram) : ImmutableClustering =
            // sort cells array depending on coalesce direction:
            // 1. coalesce vertically means sort horizontally (small to large x values)
            // 2. coalesce horizontally means sort vertically (small to large y values)
            let cells =
                clusters
                |> Seq.map (fun cluster -> cluster |> Seq.toArray)
                |> Array.concat 
                |> Array.sortBy (fun addr -> if coal_vert then (addr.X, addr.Y) else (addr.Y, addr.X))

            // algorithm mutates clusters'
            let clusters' = ToMutableClustering clusters

            let revLookup = ReverseClusterLookupMutable clusters'

            let adjacent = if coal_vert then
                                (fun (c1: AST.Address)(c2: AST.Address) -> c1.X = c2.X && c1.Y < c2.Y)
                            else
                                (fun (c1: AST.Address)(c2: AST.Address) -> c1.Y = c2.Y && c1.X < c2.X)

            for i in 0 .. cells.Length - 2 do
                // get cell
                let cell = cells.[i]
                // get cluster of the cell
                let source = revLookup.[cell]
                // get possibly adjacent cell
                let maybeAdj = cells.[i+1]

                // if maybeAdj is already in the same cluster, move on
                if not (source.Contains maybeAdj) then
                    // cell's countable
                    let (_,_,co_cell) = hb_inv.[cell]
                    // maybeAdj's countable
                    let (_,_,co_maybe) = hb_inv.[maybeAdj]
                    // is maybeAdj adjacent to cell and has the same cvector?
                    let isAdj = adjacent cell maybeAdj
                    let sameCV = co_cell.ToCVectorResultant = co_maybe.ToCVectorResultant
                    if isAdj && sameCV then
                        // get cluster for maybeAdj
                        let target = revLookup.[maybeAdj]
                        // if I merge source and target, does the merge remain rectangular?
                        let isRect = BinaryMinEntropyTree.MergeIsRectangular source target
                        if isRect then
                            // add every cell from source to target hashset
                            HashSetUtils.inPlaceUnion source target
                            // update all reverse lookups for source cells
                            for c in source do
                                revLookup.[c] <- target
                            // remove source from clusters
                            clusters'.Remove source |> ignore

            ToImmutableClustering clusters'

        static member RectangularClustering(tree: BinaryMinEntropyTree)(hb_inv: ROInvertedHistogram) : ImmutableClustering =
            // coalesce all cells that have the same cvector,
            // ensuring that all merged clusters remain rectangular
            let regs = BinaryMinEntropyTree.Regions tree
            let clusters = regs |> Array.map (fun leaf -> leaf.Cells) |> makeImmutableGenericClustering

            BinaryMinEntropyTree.RectangularCoalesce clusters hb_inv

        static member RectangularCoalesce(cs: ImmutableClustering)(hb_inv: ROInvertedHistogram) : ImmutableClustering =
            let mutable clusters = cs
            let mutable changed = true

            let mutable timesAround = 1

            while changed do
                // coalesce vertical ordering horizontally
                let clusters' = BinaryMinEntropyTree.CoaleseAdjacentClusters false clusters hb_inv

                // coalesce horizontal ordering vertically
                let clusters'' = BinaryMinEntropyTree.CoaleseAdjacentClusters true clusters' hb_inv

                if CommonFunctions.SameClustering clusters clusters'' then
                    changed <- false
                else
                    if timesAround > 10000 then
                        failwith "Coalesce convergence error."

                    changed <- true
                    clusters <- clusters''

                timesAround <- timesAround + 1

            // return clustering
            clusters

    and Inner(lefttop: AST.Address, rightbottom: AST.Address, subtree: SubtreeKind) =
        inherit BinaryMinEntropyTree(lefttop, rightbottom, subtree)
        let mutable left = None
        let mutable right = None
        member self.AddLeft(n: BinaryMinEntropyTree) : unit =
            left <- Some n
        member self.AddRight(n: BinaryMinEntropyTree) : unit =
            right <- Some n
        member self.Left =
            match left with
            | Some l -> l
            | None -> failwith "Cannot traverse tree until it is constructed!"
        member self.Right =
            match right with
            | Some r -> r
            | None -> failwith "Cannot traverse tree until it is constructed!"
        override self.ToGraphViz(i: int) =
            let start = "\"" + i.ToString() + "\""
            let start_node = start + " [label=\"" + self.Region + "\"]\n"
            let (j,ledge) = match left with
                            | Some l ->
                                let (i',graph) = l.ToGraphViz (i + 1)
                                i', start + " -- " + "\"" + (i + 1).ToString() + "\"" + "\n" + graph
                            | None -> i,""
            let (k,redge) = match right with
                            | Some r ->
                                let (j',graph) = r.ToGraphViz (j + 1)
                                j', start + " -- " + "\"" + (j + 1).ToString() + "\"" + "\n" + graph
                            | None -> j,""
            k,start_node + ledge + redge

    and Leaf(lefttop: AST.Address, rightbottom: AST.Address, subtree: SubtreeKind, cells: Cells) =
        inherit BinaryMinEntropyTree(lefttop, rightbottom, subtree)
        member self.Cells : ImmutableHashSet<AST.Address> = (new HashSet<AST.Address>(cells.Keys)).ToImmutableHashSet()
        override self.ToGraphViz(i: int)=
            let start = "\"" + i.ToString() + "\""
            let node = start + " [label=\"" + self.Region + "\"]\n"
            i,node

    and SubtreeKind =
    | LeftOf of Inner
    | RightOf of Inner
    | Root