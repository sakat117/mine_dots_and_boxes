module MineDotsAndBoxesAvalonia.Program

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI
open MineDotsAndBoxesAvalonia.MainWindow

// Use aliases to avoid ambiguity
module ElmishProgram = Elmish.Program
module FuncUIProgram = Avalonia.FuncUI.Elmish.Program

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Mine Dots and Boxes (Avalonia)"
        base.Width <- 800.0
        base.Height <- 800.0
        
        ElmishProgram.mkProgram init update view
        |> FuncUIProgram.withHost this
        |> ElmishProgram.run

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add (FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Light

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

[<EntryPoint>]
let main(args: string[]) =
    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .UseSkia()
        .StartWithClassicDesktopLifetime(args)