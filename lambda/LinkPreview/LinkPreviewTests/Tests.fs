namespace LinkPreviewTests

open Xunit
open LinkPreviewCore

type PreviewTests() =

    [<Fact>]
    member _.``Should fetch title from example.com`` () =
        let result =
            PreviewService.fetchPreview("https://example.com")
            |> Async.AwaitTask
            |> Async.RunSynchronously
        printfn "Title: %A" result.Title
        Assert.Equal("https://example.com", result.Url)
        Assert.True(result.Title.IsSome)