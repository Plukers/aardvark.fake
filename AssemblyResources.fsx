﻿#nowarn "211"
#I @"..\..\..\..\packages\build"
#I @"packages"
#r @"FAKE\tools\FakeLib.dll"
#r @"Paket.Core\lib\net45\Paket.Core.dll"
#r @"Mono.Cecil\lib\net45\Mono.Cecil.dll"
#r @"Chessie\lib\net40\Chessie.dll"
#r @"System.IO.Compression.dll"
#r @"System.IO.Compression.FileSystem.dll"

namespace Aardvark.Fake

open System
open System.Reflection
open System.IO
open System.IO.Compression
open Fake

module AssemblyResources =
    open System
    open Mono.Cecil
    open System.IO
    open System.IO.Compression
    open System.Collections.Generic

    let rec addFolderToArchive (path : string) (folder : string) (archive : ZipArchive) =
        let files = Directory.GetFiles(folder)
        for f in files do
            let e = archive.CreateEntryFromFile(f, Path.Combine(path, Path.GetFileName f))
            ()

        let sd = Directory.GetDirectories(folder)
        for d in sd do
            let p = Path.Combine(path, Path.GetFileName d)
            addFolderToArchive p d archive

    let addFolder (folder : string) (assemblyPath : string) =
        let symbols = File.Exists (Path.ChangeExtension(assemblyPath, "pdb"))

        let a = AssemblyDefinition.ReadAssembly(assemblyPath,ReaderParameters(ReadSymbols=symbols))

        let mem = new MemoryStream()
        let archive = new ZipArchive(mem, ZipArchiveMode.Create, true)
        addFolderToArchive "" folder archive

        // remove the old resource (if any)
        let res = a.MainModule.Resources |> Seq.tryFind (fun r -> r.Name = "native.zip")
        match res with
            | Some res -> a.MainModule.Resources.Remove res |> ignore
            | None -> ()

        // create and add the new resource
        archive.Dispose()
        mem.Position <- 0L
        let data = mem.ToArray()
        let r = EmbeddedResource("native.zip", ManifestResourceAttributes.Public, data)
    
        mem.Dispose()

        a.MainModule.Resources.Add(r)
        tracefn "added native resources to %A" (Path.GetFileName assemblyPath)
        a.Write( assemblyPath, WriterParameters(WriteSymbols=symbols))

        ()

    let getFilesAndFolders (folder : string) =
        if Directory.Exists folder then Directory.GetFileSystemEntries folder
        else [||]

    let copyDependencies (folder : string) (targets : seq<string>) =
        let arch = 
            "AMD64" // developer machines are assumed to be 64 bit machines

        let platform =
            match Environment.OSVersion.Platform with
                | PlatformID.MacOSX -> "mac"
                | PlatformID.Unix -> "linux"
                | _ -> "windows"

        for t in targets do
            getFilesAndFolders(Path.Combine(folder, platform, arch)) 
                |> CopyFiles t

            getFilesAndFolders(Path.Combine(folder, platform)) 
                |> Array.filter (fun f -> 
                    let n = Path.GetFileName(f) 
                    n <> "x86" && n <> "AMD64"
                    )
                |> CopyFiles t

            getFilesAndFolders(Path.Combine(folder, arch)) 
                |> CopyFiles t

            getFilesAndFolders(folder) 
                |> Array.filter (fun f -> 
                    let n = Path.GetFileName(f) 
                    n <> "x86" && n <> "AMD64" && n <> "windows" && n <> "linux" && n <> "mac"
                    )
                |> CopyFiles t

