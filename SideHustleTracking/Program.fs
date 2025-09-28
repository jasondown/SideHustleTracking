module SideHustleTracking.App

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types
open SideHustleTracking.Persistence.Csv

// Test persistence
let testPersistence () =
    // Create a test entry
    let testEntry =
        Open { Id = EntryId (Guid.NewGuid())
               Date = DateOnly(2024, 1, 15)
               Start = TimeOnly(9, 0)
               UsdRate = 50m<rate>
               FxCadPerUsd = 1.35m<fx> }
    
    // Append it
    appendEntry testEntry
    
    // Read all entries
    let entries = readEntries ()

    $"Persistence test: %d{entries.Length} entries in CSV"

// Handlers
let indexHandler : HttpHandler =
    let persistenceResult = testPersistence ()
    text $"SideHustleTracking - {persistenceResult}"

let webApp =
    choose [
        GET >=> route "/" >=> indexHandler
        setStatusCode 404 >=> text "Not Found"
    ]

// Configuration
let configureApp (app : IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .Configure(configureApp)
                .ConfigureServices(configureServices)
                |> ignore)
        .Build()
        .Run()
    0
