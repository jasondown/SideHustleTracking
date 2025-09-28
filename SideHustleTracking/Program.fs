module SideHustleTracking.App

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open FSharpPlus.Data
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.Calculations

// Quick test of domain logic
let testDomain () =
    let openEntry = {
        Id = EntryId (Guid.NewGuid())
        Date = DateOnly(2024, 1, 15)
        Start = TimeOnly(9, 0)
        UsdRate = 50m<rate>
        FxCadPerUsd = 1.35m<fx>
    }
    
    let result = close openEntry (TimeOnly(11, 30))
    match result with
    | Success closed ->
        $"Test passed: %.2f{closed.Hours / 1m<h>} hours, $%.2f{closed.TotalCad / 1m<CAD>} CAD"
    | Failure errors -> 
        sprintf "Test failed: %s" (String.concat ", " errors)

// Handlers
let indexHandler : HttpHandler =
    let testResult = testDomain()
    text $"SideHustleTracking - Domain Test: {testResult}"

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
