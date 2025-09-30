module SideHustleTracking.Views.Entries

open System
open Giraffe.ViewEngine
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Entry

// Helper to create htmx attributes
let private _hx (name: string) (value: string) = attr ("hx-" + name) value

let private formatTime (time: TimeOnly) = time.ToString("HH:mm")
let private formatDate (date: DateOnly) = date.ToString("yyyy-MM-dd")
let private formatDecimal (value: decimal) = value.ToString("F2")

// Helper to strip units for display (divide by 1 with the unit)
let private stripRate (r: decimal<rate>) : decimal = r / 1m<rate>
let private stripFx (fx: decimal<fx>) : decimal = fx / 1m<fx>
let private stripHours (h: decimal<h>) : decimal = h / 1m<h>
let private stripCad (c: decimal<CAD>) : decimal = c / 1m<CAD>

let entryRow (entry: Entry) =
    match entry with
    | Open o ->
        tr [ _class "open-entry" ] [
            td [] [ str (formatDate o.Date) ]
            td [] [ str (formatTime o.Start) ]
            td [] [ str "-" ]
            td [] [ str (sprintf "$%s" (formatDecimal (stripRate o.UsdRate))) ]
            td [] [ str (formatDecimal (stripFx o.FxCadPerUsd)) ]
            td [] [ str "-" ]
            td [] [ str "-" ]
            td [] [ 
                let (EntryId guid) = o.Id
                button [ 
                    _hx "post" (sprintf "/entries/%s/close" (guid.ToString()))
                    _hx "target" "closest tr"
                    _hx "swap" "outerHTML"
                ] [ str "Stop" ] 
            ]
        ]
    | Closed c ->
        tr [ _class "closed-entry" ] [
            td [] [ str (formatDate c.Date) ]
            td [] [ str (formatTime c.Start) ]
            td [] [ str (formatTime c.End) ]
            td [] [ str (sprintf "$%s" (formatDecimal (stripRate c.UsdRate))) ]
            td [] [ str (formatDecimal (stripFx c.FxCadPerUsd)) ]
            td [] [ str (formatDecimal (stripHours c.Hours)) ]
            td [] [ str (sprintf "$%s" (formatDecimal (stripCad c.TotalCad))) ]
            td [] [ str "Completed" ]
        ]

let entriesTable (entries: Entry list) =
    table [] [
        thead [] [
            tr [] [
                th [] [ str "Date" ]
                th [] [ str "Start" ]
                th [] [ str "End" ]
                th [] [ str "Rate (USD/h)" ]
                th [] [ str "FX Rate (CAD/USD)" ]
                th [] [ str "Hours" ]
                th [] [ str "Total (CAD)" ]
                th [] [ str "Actions" ]
            ]
        ]
        tbody [ _id "entries-tbody" ] (entries |> List.map entryRow)
    ]

let addEntryForm (errors: string list option) =
    let now = DateTime.Now
    form [ _hx "post" "/entries"; _hx "target" "#entries-tbody"; _hx "swap" "afterbegin" ] [
        h2 [] [ str "Start New Entry" ]
        
        // Display errors if any
        match errors with
        | Some errs -> 
            div [ _class "error" ] (errs |> List.map (fun e -> p [] [ str e ]))
        | None -> ()
        
        div [ _class "form-group" ] [
            label [ _for "date" ] [ str "Date" ]
            input [ _type "date"; _name "date"; _id "date"; _value (now.ToString("yyyy-MM-dd")); _required ]
        ]
        
        div [ _class "form-group" ] [
            label [ _for "start" ] [ str "Start Time" ]
            input [ _type "time"; _name "start"; _id "start"; _value (now.ToString("HH:mm")); _required ]
        ]
        
        div [ _class "form-group" ] [
            label [ _for "usdRate" ] [ str "Hourly Rate (USD)" ]
            input [ _type "number"; _name "usdRate"; _id "usdRate"; _step "0.01"; _min "0"; _required ]
        ]
        
        div [ _class "form-group" ] [
            label [ _for "fxRate" ] [ str "Exchange Rate (CAD/USD)" ]
            input [ _type "number"; _name "fxRate"; _id "fxRate"; _step "0.0001"; _min "0"; _value "1.35"; _required ]
        ]
        
        button [ _type "submit" ] [ str "Start Entry" ]
    ]

let indexView (entries: Entry list) =
    Layout.layout "Time Entries" [
        h1 [] [ str "Side Hustle Time Tracker" ]
        
        addEntryForm None
        
        h2 [] [ str "All Entries" ]
        if List.isEmpty entries then
            p [] [ str "No entries yet. Start tracking your time!" ]
        else
            entriesTable entries
    ]
