module SideHustleTracking.Views.Entries

open System
open Giraffe.ViewEngine
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.UnitsOfMeasure

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
let private stripUsd (u: decimal<USD>) : decimal = u / 1m<USD>

let entryRow (entry: Entry) =
    match entry with
    | Open o ->
        let (EntryId guid) = o.Id

        tr
            [ _class "open-entry" ]
            [ td [] [ str (formatDate o.Date) ]
              td [] [ str (formatTime o.Start) ]
              td [] [ str "-" ]
              td [] [ str $"$%s{formatDecimal (stripRate o.UsdRate)}" ]
              td [] [ str (formatDecimal (stripFx o.FxCadPerUsd)) ]
              td [] [ str "-" ]
              td [] [ str "-" ]
              td
                  []
                  [ button
                        [ _hx "post" $"/entries/{guid.ToString()}/close"
                          _hx "target" "#entries-list" // Changed from #entries-tbody
                          _hx "swap" "outerHTML"
                          _style "margin-right: 5px;" ]
                        [ str "Stop" ]
                    button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/edit"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "margin-right: 5px;" ]
                        [ str "Edit" ]
                    button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/delete/confirm"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "background: #dc3545;" ]
                        [ str "Delete" ] ] ]
    | Closed c ->
        let (EntryId guid) = c.Id

        tr
            [ _class "closed-entry" ]
            [ td [] [ str (formatDate c.Date) ]
              td [] [ str (formatTime c.Start) ]
              td [] [ str (formatTime c.End) ]
              td [] [ str $"$%s{formatDecimal (stripRate c.UsdRate)}" ]
              td [] [ str (formatDecimal (stripFx c.FxCadPerUsd)) ]
              td [] [ str (formatDecimal (stripHours c.Hours)) ]
              td [] [ str $"$%s{formatDecimal (stripCad c.TotalCad)}" ]
              td
                  []
                  [ button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/edit"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "margin-right: 5px;" ]
                        [ str "Edit" ]
                    button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/delete/confirm"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "background: #dc3545;" ]
                        [ str "Delete" ] ] ]

let entryEditFormRow (entry: Entry) =
    let entryId, date, start, endTimeOpt, usdRate, fxRate =
        match entry with
        | Open o -> (o.Id, o.Date, o.Start, None, o.UsdRate, o.FxCadPerUsd)
        | Closed c -> (c.Id, c.Date, c.Start, Some c.End, c.UsdRate, c.FxCadPerUsd)

    let (EntryId guid) = entryId
    let endTimeValue = endTimeOpt |> Option.map formatTime |> Option.defaultValue ""

    tr
        []
        [ td
              [ _colspan "8" ]
              [ form
                    [ _hx "post" "/entries/update"
                      _hx "target" "#entries-list"
                      _hx "swap" "outerHTML" ]
                    [ input [ _type "hidden"; _name "id"; _value (guid.ToString()) ]

                      div
                          [ _style "display: flex; gap: 10px; align-items: end;" ]
                          [ div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "date" ] [ str "Date" ]
                                  input [ _type "date"; _name "date"; _value (formatDate date); _required ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "start" ] [ str "Start" ]
                                  input [ _type "time"; _name "start"; _value (formatTime start); _required ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "endTime" ] [ str "End" ]
                                  input [ _type "time"; _name "endTime"; _value endTimeValue ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "usdRate" ] [ str "Rate (USD)" ]
                                  input
                                      [ _type "number"
                                        _name "usdRate"
                                        _step "0.01"
                                        _min "0"
                                        _value (string (stripRate usdRate))
                                        _required ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "fxRate" ] [ str "FX Rate" ]
                                  input
                                      [ _type "number"
                                        _name "fxRate"
                                        _step "0.0001"
                                        _min "0"
                                        _value (string (stripFx fxRate))
                                        _required ] ]

                            button [ _type "submit"; _style "margin-right: 5px;" ] [ str "Save" ]
                            button
                                [ _type "button"
                                  _hx "get" $"/entries/%s{guid.ToString()}/cancel"
                                  _hx "target" "closest tr"
                                  _hx "swap" "outerHTML" ]
                                [ str "Cancel" ] ] ] ] ]

let entryDeleteConfirmRow (entry: Entry) =
    let entryId, date, start, endTimeOpt =
        match entry with
        | Open o -> (o.Id, o.Date, o.Start, None)
        | Closed c -> (c.Id, c.Date, c.Start, Some c.End)

    let (EntryId guid) = entryId

    let entryDesc =
        match endTimeOpt with
        | Some endTime -> $"%s{formatDate date} from %s{formatTime start} to %s{formatTime endTime}"
        | None -> $"%s{formatDate date} starting at %s{formatTime start} (open)"

    tr
        [ _class "error" ]
        [ td
              [ _colspan "8"; _style "padding: 20px;" ]
              [ p [ _style "margin: 0 0 10px 0; font-weight: bold;" ] [ str $"Delete this entry: %s{entryDesc}?" ]
                button
                    [ _hx "post" $"/entries/%s{guid.ToString()}/delete"
                      _hx "target" "#entries-list"
                      _hx "swap" "outerHTML"
                      _style "margin-right: 10px; background: #dc3545;" ]
                    [ str "Yes, Delete" ]
                button
                    [ _hx "get" $"/entries/%s{guid.ToString()}/cancel"
                      _hx "target" "closest tr"
                      _hx "swap" "outerHTML" ]
                    [ str "Cancel" ] ] ]


let entriesTable (entries: Entry list) =
    table
        []
        [ thead
              []
              [ tr
                    []
                    [ th [] [ str "Date" ]
                      th [] [ str "Start" ]
                      th [] [ str "End" ]
                      th [] [ str "Rate (USD/h)" ]
                      th [] [ str "FX Rate (CAD/USD)" ]
                      th [] [ str "Hours" ]
                      th [] [ str "Total (CAD)" ]
                      th [] [ str "Actions" ] ] ]
          tbody [ _id "entries-tbody" ] (entries |> List.map entryRow) ]

let entriesListView (entries: Entry list) =
    div
        [ _id "entries-list" ]
        [ if List.isEmpty entries then
              p [] [ str "No entries yet. Start tracking your time!" ]
          else
              entriesTable entries ]

let addEntryForm (errors: string list option) =
    let now = DateTime.Now

    form
        [ _hx "post" "/entries"; _hx "target" "#entries-list"; _hx "swap" "outerHTML" ]
        [ h2 [] [ str "Add Entry" ]

          // Display errors if any
          match errors with
          | Some errs -> div [ _class "error" ] (errs |> List.map (fun e -> p [] [ str e ]))
          | None -> ()

          div
              [ _class "form-group" ]
              [ label [ _for "date" ] [ str "Date" ]
                input
                    [ _type "date"
                      _name "date"
                      _id "date"
                      _value (now.ToString("yyyy-MM-dd"))
                      _required ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "start" ] [ str "Start Time" ]
                input
                    [ _type "time"
                      _name "start"
                      _id "start"
                      _value (now.ToString("HH:mm"))
                      _required ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "endTime" ] [ str "End Time (optional - leave blank to start tracking now)" ]
                input [ _type "time"; _name "endTime"; _id "endTime" ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "usdRate" ] [ str "Hourly Rate (USD)" ]
                input
                    [ _type "number"
                      _name "usdRate"
                      _id "usdRate"
                      _step "0.01"
                      _min "0"
                      _required ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "fxRate" ] [ str "Exchange Rate (CAD/USD)" ]
                input
                    [ _type "number"
                      _name "fxRate"
                      _id "fxRate"
                      _step "0.0001"
                      _min "0"
                      _value "1.35"
                      _required ] ]

          button [ _type "submit" ] [ str "Add Entry" ] ]

let indexView (entries: Entry list) =
    Layout.layout
        "Time Entries"
        [ h1 [] [ str "Side Hustle Time Tracker" ]

          addEntryForm None

          h2 [] [ str "All Entries" ]
          entriesListView entries ] // Use the new function
