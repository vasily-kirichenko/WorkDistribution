#r @"packages\Hopac\lib\net45\Hopac.Platform.dll"
#r @"packages\Hopac\lib\net45\Hopac.Core.dll"
#r @"packages\Hopac\lib\net45\Hopac.dll"

open Hopac
open System

let callService url input =
    job {
        Console.WriteLine (sprintf "Calling %s with %s..." url input)
        do! timeOutMillis 500
        return input
    }

let process' machine input =
    let url = sprintf "http://%s/webservice/endpoint" machine

    job {
        // call a web service with a given input (file)
        return! callService url input
    }

let renditions (machines: string list) (inputs: string[]) =
    job {
        let! inputCh = Ch.create()
        let! finished = IVar.create()

        do! job { for input in inputs do
                    do! Ch.give inputCh input
                    Console.WriteLine (sprintf "%s has been given to input channel." input) }
            |> Job.queue

        let! resultVar = MVar.createFull (ResizeArray())

        let worker machine =
            Job.foreverServer <| 
                job {
                    let! input = Ch.take inputCh
                    let! res = process' machine input 
                    let! results = MVar.take resultVar
                    results.Add res
                    do! MVar.fill resultVar results
                    if results.Count = inputs.Length then
                        do! IVar.fill finished ()
                }

        let workersCount = 4

        for machine in machines do
            for _ in 1..workersCount do
                do! worker machine |> Job.queue

        do! IVar.read finished
        return! MVar.take resultVar |> Job.map Seq.toList
    }
    |> run

renditions ["host1"; "host2"] (Array.init 10 string)
