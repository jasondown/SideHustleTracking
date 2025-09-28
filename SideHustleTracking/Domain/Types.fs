module SideHustleTracking.Domain.Types

open System
open SideHustleTracking.Domain.UnitsOfMeasure

// Core value types
type EntryId = EntryId of Guid
type LocalDate = DateOnly
type LocalTime = TimeOnly

// Domain types
type OpenInterval =
    { Id: EntryId
      Date: LocalDate
      Start: LocalTime
      UsdRate: decimal<rate>
      FxCadPerUsd: decimal<fx> }

type ClosedInterval =
    { Id: EntryId
      Date: LocalDate
      Start: LocalTime
      End: LocalTime
      UsdRate: decimal<rate>
      FxCadPerUsd: decimal<fx>
      Hours: decimal<h>
      TotalCad: decimal<CAD> }

type Entry =
    | Open of OpenInterval
    | Closed of ClosedInterval
