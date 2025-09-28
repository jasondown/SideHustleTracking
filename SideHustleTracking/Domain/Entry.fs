module SideHustleTracking.Domain.Entry

open SideHustleTracking.Domain.Types
    
let getId = function
    | Open o -> o.Id
    | Closed c -> c.Id

let getDate = function
    | Open o -> o.Date
    | Closed c -> c.Date

let isOpen = function
    | Open _ -> true
    | Closed _ -> false

let isClosed = function
    | Open _ -> false
    | Closed _ -> true
