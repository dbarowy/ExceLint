﻿open COMWrapper
open ExceLint
open ExceLint.Utils
open ExceLintFileFormats
open System.Collections.Generic
open System.Diagnostics
open System.Threading
open System.Text.RegularExpressions

type FDict = Dictionary<AST.Address,string>
type FCount = Dictionary<string,int>
type ParseOKorNot =
| Success of AST.Expression
| PFailure of ParserErrorsRow
| TFailure of ExceptionLogRow
type WorkerFn = Application -> CorpusStats -> ParserErrors -> ExceptionLog -> unit

let TIMEOUT_S = 20
let ENUFWORK = 20

// Adapted from: http://stackoverflow.com/questions/9460661/implementing-regex-timeout-in-net-4/9461311#9461311
let WithTimeout(proc: unit -> 'a)(duration_ms: int) =
    let reset = new AutoResetEvent false
    let mutable ex: System.Exception option = None
    let mutable retVal: 'a option = None
    
    let ts = new ThreadStart(
                (fun () ->
                    try
                        retVal <- Some(proc())
                    with
                    | e -> ex <- Some e

                    reset.Set() |> ignore
                )
              )

    let t = new Thread(ts)
    t.Start()

    if not (reset.WaitOne(duration_ms)) then
        t.Abort()
        raise (System.TimeoutException())

    match ex with
    | Some e -> raise e
    | None ->
        match retVal with
        | Some r -> r
        | None -> failwith "Unexpected failure."

let asts(workbook: string)(fsd: FDict)(err: ParserErrors)(exlog: ExceptionLog)(ucount: int byref) : AST.Expression[] =
    let fsda = Seq.toArray fsd |> Array.sortBy (fun pair -> pair.Value.Length)

    let mutable i = 0
    
    let do_work(pair: KeyValuePair<AST.Address,string>) : ParseOKorNot = 
        let addr = pair.Key
        let astr = pair.Value
        try
            Success(WithTimeout (fun () -> Parcel.parseFormulaAtAddress addr astr) TIMEOUT_S )
        with
        | :? System.TimeoutException ->
            let exrow = new ExceptionLogRow()
            exrow.Workbook <- workbook
            exrow.Error <- "Timeout"

            printfn "Analysis timeout: %A" workbook

            TFailure exrow
        | ex ->
            // log error as a side-effect
            let erow = new ParserErrorsRow()
            erow.Path <- addr.Path
            erow.Workbook <- addr.WorkbookName
            erow.Worksheet <- addr.WorksheetName
            erow.Address <- addr.A1Local()
            erow.Formula <- astr

            printfn "Parser failure: cell %A in %A" (addr.A1Local()) workbook

            // we failed; return row
            PFailure erow

    let chooser =
        fun e ->
            // this function is not thread-safe
            // because CsvHelper is NOT thread-safe!
            match e with
            | Success ast -> Some ast
            | PFailure erow ->
                
                err.WriteRow erow
                i <- i + 1
                None
            | TFailure exrow ->
                exlog.WriteRow exrow
                i <- i + 1
                None

    // do work in parallel if there is enough of it
    let output =
        fsda
            |> if fsda.Length > ENUFWORK then
                   Array.Parallel.map do_work
               else
                   Array.map do_work
            |> Seq.choose chooser
            |> Seq.toArray

    ucount <- i
    output

let ast_count(fs_asts: AST.Expression[]) : FCount =
    fs_asts |>
        Array.fold (fun (acc: Dictionary<string,int>)(ast: AST.Expression) ->
            let strs = Parcel.operatorNamesFromExpr ast
            for str in strs do
                if not (acc.ContainsKey str) then
                    acc.Add(str, 0)
                acc.[str] <- acc.[str] + 1
            acc
        ) (new Dictionary<string,int>())

let workbook_proper_name(workbook: string)(tmpdir: string) : string =
    let r = new Regex(".+\.xlsx?", RegexOptions.IgnoreCase)
    if r.IsMatch workbook then
        System.IO.Path.Combine(
                            tmpdir,
                            System.IO.Path.GetFileName(workbook)
                            )
    else
        // only append extension if it is missing
        match Application.MagicBytes(workbook) with
        | Application.CWFileType.XLS ->
            let newpath = System.IO.Path.Combine(
                            tmpdir,
                            System.IO.Path.GetFileName(workbook) + ".xls"
                            )
            newpath
        | Application.CWFileType.XLSX ->
            let newpath = System.IO.Path.Combine(
                            tmpdir,
                            System.IO.Path.GetFileName(workbook) + ".xlsx"
                            )
            newpath
        | _ ->
            printfn "Not an Excel file: %A" workbook
            failwith (sprintf "Not an Excel file: %A" workbook)

let open_and_do_stuff(config: Args.Config)(dothis: WorkerFn) : unit =
    using(new Application()) (fun app ->
        using(new CorpusStats(config.output_file)) (fun csv ->
            using(new ParserErrors(config.error_file, csv.WasResumed)) (fun err ->
                using(new ExceptionLog(config.exception_file, csv.WasResumed)) (fun exlog ->
                    dothis app csv err exlog
                )
            )
        )
    )

[<EntryPoint>]
let main argv = 
    let config =
            try
                Args.processArgs argv
            with
            | e ->
                printfn "%A" e.Message
                System.Environment.Exit 1
                failwith "never gets called but keeps F# happy"

    let workbooks = Seq.map (fun fname ->
                                  let wbname = System.IO.Path.GetFileName fname
                                  let path = System.IO.Path.GetDirectoryName fname
                                  System.IO.Path.Combine(path, wbname)
                             ) (config.files)

    let tmpdir = System.IO.Path.GetTempPath()

    // this is what the worker does
    let workerfn: WorkerFn = fun app csv err exlog ->
        for workbook in workbooks do
                try
                    // determine file type and name at tmp dir
                    let workbook' = workbook_proper_name workbook tmpdir

                    if csv.IsProcessed(workbook') then
                        printfn "Already processed: %A" workbook
                    else
                        printfn "Opening: %A" workbook

                        // copy to tmp dir
                        System.IO.File.Copy(workbook, workbook', overwrite = true)

                        // get to it
                        try
                            using(app.OpenWorkbook(workbook')) (fun wb ->
                                let sw = new Stopwatch()
                                sw.Start()

                                printfn "Reading workbook formulas: %A" workbook'
                                let fsd = wb.Formulas;

                                // get all formula ASTs
                                let mutable ucount = 0
                                let fs_asts = asts workbook fsd err exlog &ucount

                                sw.Stop()

                                // get operator counts from ASTs
                                let ops = ast_count fs_asts
                    
                                // add unparseable formula count
                                if ucount > 0 then
                                    ops.Add("unparseable",ucount)
            
                                // write rows to CSV, one per operator
                                for pair in ops do
                                    let row = new CorpusStatsRow()
                                    row.Workbook <- System.IO.Path.GetFileName workbook'
                                    row.Variable <- pair.Key
                                    row.Value <- int64 (pair.Value)
                                    csv.WriteRow row

                                // write the total number of formulas
                                let row = new CorpusStatsRow()
                                row.Workbook <- System.IO.Path.GetFileName workbook'
                                row.Variable <- "num_formulas"
                                row.Value <- int64 (fsd.Count)
                                csv.WriteRow row

                                // record time
                                let row = new CorpusStatsRow()
                                row.Workbook <- System.IO.Path.GetFileName workbook'
                                row.Variable <- "analysis_time_ms"
                                row.Value <- sw.ElapsedMilliseconds
                                csv.WriteRow row

                                // let the user know we're done
                                printfn "Workbook analysis complete; took %A milliseconds." (sw.ElapsedMilliseconds.ToString())

                                // mark as processed
                                csv.MarkAsProcessed workbook'
                            )
                        finally
                            System.IO.File.Delete workbook'
                    with
                    | ex ->
                        let exrow = new ExceptionLogRow()
                        exrow.Workbook <- workbook
                        exrow.Error <- ex.Message
                        exlog.WriteRow exrow

                        printfn "Cannot open workbook: %A" workbook

    // do the work now
    open_and_do_stuff config workerfn

    printfn "Press any key to continue..."
    System.Console.ReadKey() |> ignore

    0 // exit normally
