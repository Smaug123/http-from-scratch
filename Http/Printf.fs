namespace Http

/// Random helpers for AOT-friendly printf
[<RequireQualifiedAccess>]
module Printfn =

    let int (format : string) (value : int) =
        System.Console.WriteLine (format + " " + value.ToString ())

    let inline time (format : string) (ms : int64) =
        System.Console.WriteLine (format + " " + ms.ToString () + "ms")
