namespace Http

open System.Text

// HTTP/1.1 is defined in https://www.rfc-editor.org/rfc/rfc9112.html

type RequestLine =
    | OriginForm of method : string * host : string * target : string
    | AbsoluteForm of method : string * uri : string
    | AuthorityForm of host : string * port : int
    | AsteriskForm

    override this.ToString () =
        match this with
        | OriginForm (method, _host, target) -> sprintf "%s %s HTTP/1.1" method target
        | AbsoluteForm (method, uri) -> sprintf "%s %s HTTP/1.1" method uri
        | AuthorityForm (host, port) -> sprintf "CONNECT %s:%d HTTP/1.1" host port
        | AsteriskForm -> "OPTIONS * HTTP/1.1"

    member this.AddToRequest (request : ResizeArray<byte>) : unit =
        // obvious candidate to improve speed by allocating less
        this.ToString () |> Encoding.ASCII.GetBytes |> request.AddRange

type FieldLine =
    {
        Name : string
        Value : string
    }

    override this.ToString () = sprintf "%s: %s" this.Name this.Value

    member this.AddToRequest (request : ResizeArray<byte>) : unit =
        // obvious candidate to improve speed by allocating less
        this.ToString () |> Encoding.ASCII.GetBytes |> request.AddRange

type ContentTransfer =
    | ContentLength
    | TransferEncoding of string

type HttpRequest =
    {
        Request : RequestLine
        Headers : FieldLine list
        Body : (byte[] * ContentTransfer) option
    }

    member this.ToBytes () : byte[] =
        let builder = ResizeArray<byte> ()
        this.Request.AddToRequest builder
        builder.Add (byte '\r')
        builder.Add (byte '\n')

        let host =
            match this.Request with
            | OriginForm (_, host, _) -> host
            | AbsoluteForm (method, uri) ->
                failwith "I can't be bothered but here we would parse the host out of the URI"
            | AuthorityForm (host, _) -> host
            | AsteriskForm -> ""

        builder.AddRange (Encoding.ASCII.GetBytes "Host: ")
        builder.AddRange (Encoding.ASCII.GetBytes host)
        builder.Add (byte '\r')
        builder.Add (byte '\n')

        for h in this.Headers do
            h.AddToRequest builder
            builder.Add (byte '\r')
            builder.Add (byte '\n')

        match this.Body with
        | None -> ()
        | Some (_, ContentTransfer.TransferEncoding encoding) ->
            builder.AddRange (Encoding.ASCII.GetBytes "Transfer-Encoding: ")
            builder.AddRange (Encoding.ASCII.GetBytes encoding)
        | Some (body, ContentTransfer.ContentLength) ->
            builder.AddRange (Encoding.ASCII.GetBytes "Content-Length: ")
            builder.AddRange (Encoding.ASCII.GetBytes (body.Length.ToString ()))

        builder.Add (byte '\r')
        builder.Add (byte '\n')

        match this.Body with
        | None -> ()
        | Some (_body, ContentTransfer.TransferEncoding _) -> failwith "not implemented"
        | Some (body, ContentTransfer.ContentLength) -> builder.AddRange body

        builder.ToArray ()
