module SideHustleTracking.Services.FxRates

open System
open System.IO
open System.Net.Http
open FSharp.Data

// Type providers for JSON
type FxApiResponse = JsonProvider<"../data/fx/sample_fx.json">
type FxSnapshot = JsonProvider<"../data/fx/sample_fx.json">

let private httpClient = new HttpClient()

/// Ensure the fx snapshot directory exists
let private ensureFxDirExists (fxSnapshotDir: string) =
    let fullPath = Path.Combine(Directory.GetCurrentDirectory(), fxSnapshotDir)

    if not (Directory.Exists(fullPath)) then
        Directory.CreateDirectory(fullPath) |> ignore

/// Get the path for a date's snapshot file
let private snapshotPath (fxSnapshotDir: string) (date: DateOnly) =
    let filename = date.ToString("yyyy-MM-dd") + ".json"
    Path.Combine(Directory.GetCurrentDirectory(), fxSnapshotDir, filename)

/// Load a snapshot from disk if it exists
let private loadSnapshot (fxSnapshotDir: string) (date: DateOnly) : FxSnapshot.Root option =
    let path = snapshotPath fxSnapshotDir date

    if File.Exists(path) then
        try
            Some(FxSnapshot.Load(path))
        with _ ->
            None
    else
        None

/// Fetch rate from the API
let private fetchFromApi (apiBaseUrl: string) (date: DateOnly) : Async<FxApiResponse.Root option> =
    async {
        try
            let dateStr = date.ToString("yyyy-MM-dd")
            let url = $"{apiBaseUrl}/{dateStr}?from=USD&to=CAD"

            let! response = httpClient.GetStringAsync(url) |> Async.AwaitTask
            let parsed = FxApiResponse.Parse(response)

            return Some parsed
        with _ ->
            return None
    }

/// Save a snapshot to disk
let private saveSnapshot (fxSnapshotDir: string) (date: DateOnly) (response: FxApiResponse.Root) : unit =
    ensureFxDirExists fxSnapshotDir
    let path = snapshotPath fxSnapshotDir date
    File.WriteAllText(path, response.JsonValue.ToString())

/// Lookup FX rate for a date (cache-first, then API)
let lookupRate (apiBaseUrl: string) (fxSnapshotDir: string) (date: DateOnly) : Async<decimal option> =
    async {
        // Try cache first
        match loadSnapshot fxSnapshotDir date with
        | Some snapshot -> return Some(decimal snapshot.Rates.Cad)
        | None ->
            // Fetch from API
            let! apiResponse = fetchFromApi apiBaseUrl date

            // If successful, save snapshot and return rate
            match apiResponse with
            | Some response ->
                saveSnapshot fxSnapshotDir date response
                return Some(decimal response.Rates.Cad)
            | None -> return None
    }

/// Round rate to 2 decimal places for display/form
let roundRate (rate: decimal) : decimal = Math.Round(rate, 2)
