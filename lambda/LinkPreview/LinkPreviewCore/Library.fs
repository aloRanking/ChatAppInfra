namespace LinkPreviewCore


open System
open System.Net
open System.Net.Http
open HtmlAgilityPack

type LinkPreview = {
    Url: string
    Title: string option
    Description: string option
    Image: string option
    SiteName: string option
}

module PreviewService =

    let private httpClient =
        let handler = 
            
            let h = new HttpClientHandler()
            h.AllowAutoRedirect<-true
            h.MaxAutomaticRedirections<-5
            h.AutomaticDecompression<- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
            h.UseCookies<-false

            h

        // Optional fallback (ONLY if needed)
        handler.ServerCertificateCustomValidationCallback <-
            fun _ _ _ errors ->
                // Allow valid OR ignore minor SSL issues
                errors = System.Net.Security.SslPolicyErrors.None

        let client = new HttpClient(handler)

        client.Timeout <- TimeSpan.FromSeconds(5.0)

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0 Safari/537.36"
        )

        client.DefaultRequestHeaders.Accept.ParseAdd("text/html")

        client

    // 🚨 Basic SSRF protection
    let private isPrivateIp (ip: IPAddress) =
        let bytes = ip.GetAddressBytes()
        match bytes with
        | [|10uy; _; _; _|] -> true
        | [|192uy; 168uy; _; _|] -> true
        | [|172uy; b; _; _|] when b >= 16uy && b <= 31uy -> true
        | _ -> false

    let private validateUrl (url: string) =
        let uri = Uri(url)

        if uri.Scheme <> "http" && uri.Scheme <> "https" then
            failwith "Invalid URL scheme"

        let host = uri.Host
        let addresses = Dns.GetHostAddresses(host)

        if addresses |> Array.exists isPrivateIp then
            failwith "Blocked private IP"

        uri

    let private getMeta (doc: HtmlDocument) (prop: string) =
        doc.DocumentNode.SelectSingleNode($"//meta[@property='{prop}']") 
        |> fun node -> if isNull node then None else Some (node.GetAttributeValue("content", ""))

    let private getNameMeta (doc: HtmlDocument) (name: string) =
        doc.DocumentNode.SelectSingleNode($"//meta[@name='{name}']")
        |> fun node -> if isNull node then None else Some (node.GetAttributeValue("content", ""))

    let fetchPreview (url: string) = task {
        let uri = validateUrl url

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0")

        let! response = httpClient.GetStringAsync(uri)

        let doc = HtmlDocument()
        doc.LoadHtml(response)

        let title =
            match getMeta doc "og:title" with
            | Some t -> Some t
            | None ->
                let tNode = doc.DocumentNode.SelectSingleNode("//title")
                if isNull tNode then None else Some tNode.InnerText

        return {
            Url = url
            Title = title
            Description = getMeta doc "og:description" |> Option.orElse (getNameMeta doc "description")
            Image = getMeta doc "og:image"
            SiteName = getMeta doc "og:site_name"
        }
    }

// module Tests =
//     let runTest () =
//         printfn "Running link preview test..."
        
//         let result = PreviewService.fetchPreview "https://example.com" 
//                      |> Async.AwaitTask 
//                      |> Async.RunSynchronously
        
//         match result with
//             | preview ->
//                     printfn "\n✅ Preview fetched successfully:"
//                     printfn "URL: %s" preview.Url
//                     printfn "Title: %A" preview.Title
//                     printfn "Description: %A" preview.Description
//                     printfn "Image: %A" preview.Image
//                     printfn "SiteName: %A" preview.SiteName
                    
//             | _ ->
//                     printfn "❌ Failed to fetch preview"

// // This will run when the module is loaded in FSI
//     do
//         printfn "LinkPreviewCore module loaded. Run 'Tests.runTest ()' to test."
    