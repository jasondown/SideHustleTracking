module SideHustleTracking.Persistence.Csv

open System
open System.IO
open FSharp.Data
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.Serialization

[<Literal>]
let SampleCsvPath = __SOURCE_DIRECTORY__ + "/../../data/sample_entries.csv"

type EntriesCsv = CsvProvider<SampleCsvPath, HasHeaders=true>

// Get the data file path (relative to executing directory)
let private getDataPath () =
    Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "entries.csv")

// Ensure the CSV file exists with headers
let private ensureCsvExists (path: string) =
    let dir = Path.GetDirectoryName path
    if not (Directory.Exists dir) then
        Directory.CreateDirectory dir |> ignore
    
    if not (File.Exists path) then
        File.WriteAllLines(path, [| "id,date,start,end,usd_rate_per_hour,fx_cad_per_usd,hours,total_cad" |])

// Check if file ends with newline and add one if needed
let private ensureTrailingNewline (path: string) =
    if File.Exists path then
        let content = File.ReadAllText path
        if not (String.IsNullOrEmpty content) && not (content.EndsWith Environment.NewLine) then
            File.AppendAllText(path, Environment.NewLine)

// Read all entries from CSV
let readEntries () : Entry list =
    let path = getDataPath ()
    ensureCsvExists path
    
    let csv = EntriesCsv.Load(path)
    csv.Rows
    |> Seq.map (fun row ->
        // Convert from type provider inferred types to CsvRow strings
        let csvRow : CsvRow =
            { Id = row.Id.ToString()
              Date = row.Date.ToString("yyyy-MM-dd")
              Start = row.Start.ToString(@"hh\:mm")
              End = row.End |> Option.map _.ToString(@"hh\:mm")
              UsdRatePerHour = row.Usd_rate_per_hour
              FxCadPerUsd = row.Fx_cad_per_usd
              Hours = if row.Hours = 0m then None else Some row.Hours
              TotalCad = if row.Total_cad = 0m then None else Some row.Total_cad }
        fromCsvRow csvRow)
    |> Seq.toList

// Append an entry to CSV
let appendEntry (entry: Entry) : unit =
    let path = getDataPath ()
    ensureCsvExists path
    ensureTrailingNewline path  // Make sure existing file ends with newline
    
    let row = toCsvRow entry
    let line = 
        sprintf "%s,%s,%s,%s,%M,%M,%s,%s"
            row.Id
            row.Date
            row.Start
            (row.End |> Option.defaultValue "")
            row.UsdRatePerHour
            row.FxCadPerUsd
            (row.Hours |> Option.map (sprintf "%.2f") |> Option.defaultValue "0.00")
            (row.TotalCad |> Option.map (sprintf "%.2f") |> Option.defaultValue "0.00")
    
    File.AppendAllLines(path, [| line |])

// Overwrite entire CSV with list of entries
let writeEntries (entries: Entry list) : unit =
    let path = getDataPath ()
    let dir = Path.GetDirectoryName path
    if not (Directory.Exists dir) then
        Directory.CreateDirectory dir |> ignore
    
    let header = "id,date,start,end,usd_rate_per_hour,fx_cad_per_usd,hours,total_cad"
    let lines =
        entries
        |> List.map (fun entry ->
            let row = toCsvRow entry
            sprintf "%s,%s,%s,%s,%M,%M,%s,%s"
                row.Id
                row.Date
                row.Start
                (row.End |> Option.defaultValue "")
                row.UsdRatePerHour
                row.FxCadPerUsd
                (row.Hours |> Option.map (sprintf "%.2f") |> Option.defaultValue "0.00")
                (row.TotalCad |> Option.map (sprintf "%.2f") |> Option.defaultValue "0.00"))
    
    File.WriteAllLines(path, header :: lines)