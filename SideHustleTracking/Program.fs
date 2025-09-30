module SideHustleTracking.App

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.ViewEngine
open FSharpPlus.Data
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.Entry
open SideHustleTracking.Persistence.Csv
open SideHustleTracking.Views.Entries

// Form binding models
[<CLIMutable>]
type AddEntryForm = {
    date: string
    start: string
    usdRate: decimal
    fxRate: decimal
}

// Validation helpers
let parseDate (dateStr: string) : Validation<NonEmptyList<string>, DateOnly> =
    try 
        Success (DateOnly.Parse(dateStr))
    with _ -> 
        Failure (NonEmptyList.singleton "Invalid date format")

let parseTime (timeStr: string) : Validation<NonEmptyList<string>, TimeOnly> =
    try 
        Success (TimeOnly.Parse(timeStr))
    with _ -> 
        Failure (NonEmptyList.singleton "Invalid time format")

let validatePositive (name: string) (value: decimal) : Validation<NonEmptyList<string>, decimal> =
    if value > 0m then Success value
    else Failure (NonEmptyList.singleton (sprintf "%s must be positive" name))

// Handlers
let indexHandler : HttpHandler =
    fun next ctx ->
        let entries = readEntries () |> List.sortByDescending getDate
        let view = indexView entries
        htmlView view next ctx

let addEntryHandler : HttpHandler =
    fun next ctx ->
        task {
            let! form = ctx.BindFormAsync<AddEntryForm>()
            
            // Validate form data using FSharpPlus Validation
            let validation = 
                (fun date start usdRate fxRate ->
                    let entry = Open {
                        Id = EntryId (Guid.NewGuid())
                        Date = date
                        Start = start
                        UsdRate = usdRate * 1m<rate>
                        FxCadPerUsd = fxRate * 1m<fx>
                    }
                    entry)
                <!> parseDate form.date
                <*> parseTime form.start
                <*> validatePositive "USD Rate" form.usdRate
                <*> validatePositive "FX Rate" form.fxRate
            
            match validation with
            | Success entry ->
                // Save the entry
                appendEntry entry
                
                // Return just the new row for htmx to insert
                let row = entryRow entry
                return! htmlView row next ctx
                
            | Failure errors ->
                // Return error message for htmx
                let errorList = errors |> NonEmptyList.toList
                let errorView = 
                    div [ _class "error" ] [
                        for e in errorList do
                            p [] [ str e ]
                    ]
                return! htmlView errorView next ctx
        }

let webApp =
    choose [
        GET >=> route "/" >=> indexHandler
        POST >=> route "/entries" >=> addEntryHandler
        setStatusCode 404 >=> text "Not Found"
    ]

// Configuration
let configureApp (app : IApplicationBuilder) =
    app.UseStaticFiles()
       .UseGiraffe webApp

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
