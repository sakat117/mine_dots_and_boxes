module MineDotsAndBoxesAvalonia.Program

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Primitives
open Avalonia.Controls.Shapes
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Themes.Fluent
open Avalonia.Threading
// open Elmish -- Removed to avoid ambiguity

// Use aliases to avoid ambiguity
module ElmishProgram = Elmish.Program
type Cmd<'msg> = Elmish.Cmd<'msg>
module Cmd = Elmish.Cmd

// ==========================================================================================
// 1. DOMAIN (Types & Constants)
// ==========================================================================================

let availableColors = [|
    Colors.Blue; Colors.Red; Colors.Green; Colors.Orange;
    Colors.Purple; Colors.Cyan; Colors.Yellow; Colors.Black;
    Colors.Magenta; Colors.Lime; Colors.Brown; Colors.Teal;
    Colors.Pink; Colors.Gray
|]

type GamePlayerIndex = int 

type GameOrientation = 
    | Horizontal 
    | Vertical

type Position = { Row: int; Col: int }

type LinePosition = { Pos: Position; Orient: GameOrientation }

type GameConfig = {
    Rows: int
    Cols: int
    PlayerCount: int
    MineCount: int
    PlayerColors: Color[]
    LineThickness: double
    DotRadius: double
    Spacing: double
}

type GameState = {
    Config: GameConfig
    Lines: Map<LinePosition, GamePlayerIndex>
    Boxes: Map<Position, GamePlayerIndex>
    Mines: Set<Position>
    ExplodedPlayers: Set<GamePlayerIndex>
    CurrentTurn: GamePlayerIndex
    Scores: Map<GamePlayerIndex, int>
    IsGameOver: bool
    Winner: GamePlayerIndex option
}

type Screen =
    | MainMenu
    | InGame of GameState
    | Settings of GameConfig
    | ColorPicker of targetPlayer: int * editingColor: Color * returnConfig: GameConfig

// ==========================================================================================
// 2. GAME LOGIC (Pure Functions)
// ==========================================================================================

let getNextPlayer (current: GamePlayerIndex) (totalPlayers: int) (exploded: Set<GamePlayerIndex>) =
    let rec findNext p =
        let next = (p + 1) % totalPlayers
        if next = current then None 
        elif exploded.Contains next then findNext next
        else Some next
    
    match findNext current with
    | Some p -> p
    | None -> current 

let initGame (config: GameConfig) : GameState =
    let initialScores = 
        [0 .. config.PlayerCount - 1] 
        |> List.map (fun p -> (p, 0)) 
        |> Map.ofList

    let rnd = Random()
    let totalBoxes = config.Rows * config.Cols
    let allPositions = 
        [ for r in 0 .. config.Rows - 1 do
            for c in 0 .. config.Cols - 1 do
                yield { Row = r; Col = c } ]
    
    let mines = 
        allPositions
        |> List.sortBy (fun _ -> rnd.Next())
        |> List.take (Math.Min(config.MineCount, totalBoxes))
        |> Set.ofList

    {
        Config = config
        Lines = Map.empty
        Boxes = Map.empty
        Mines = mines
        ExplodedPlayers = Set.empty
        CurrentTurn = 0 
        Scores = initialScores
        IsGameOver = false
        Winner = None
    }

let getBoxEdges (pos: Position) : LinePosition list =
    [
        { Pos = pos; Orient = Horizontal }
        { Pos = { pos with Row = pos.Row + 1 }; Orient = Horizontal }
        { Pos = pos; Orient = Vertical }
        { Pos = { pos with Col = pos.Col + 1 }; Orient = Vertical }
    ]

let isBoxCompleted (lines: Map<LinePosition, GamePlayerIndex>) (pos: Position) : bool =
    let edges = getBoxEdges pos
    edges |> List.forall (fun edge -> lines.ContainsKey edge)

let findNewlyCompletedBoxes (lines: Map<LinePosition, GamePlayerIndex>) (newLine: LinePosition) (rows: int) (cols: int) : Position list =
    let potentialBoxes = 
        match newLine.Orient with
        | Horizontal -> 
            [ 
                { Row = newLine.Pos.Row; Col = newLine.Pos.Col }
                { Row = newLine.Pos.Row - 1; Col = newLine.Pos.Col }
            ]
        | Vertical -> 
            [ 
                { Row = newLine.Pos.Row; Col = newLine.Pos.Col }
                { Row = newLine.Pos.Row; Col = newLine.Pos.Col - 1 }
            ]
    
    potentialBoxes
    |> List.filter (fun p -> p.Row >= 0 && p.Row < rows && p.Col >= 0 && p.Col < cols)
    |> List.filter (fun p -> isBoxCompleted lines p)

let updateGameStatus (state: GameState) : GameState =
    let survivors = 
        [0 .. state.Config.PlayerCount - 1]
        |> List.filter (fun p -> not (state.ExplodedPlayers.Contains p))
        
    let newScores = 
        [0 .. state.Config.PlayerCount - 1]
        |> List.map (fun pid -> 
            let score = state.Boxes |> Map.filter (fun _ owner -> owner = pid) |> Map.count
            (pid, score))
        |> Map.ofList
    
    let totalBoxes = state.Config.Rows * state.Config.Cols
    let currentTotalScore = newScores |> Map.fold (fun acc _ s -> acc + s) 0
    
    let survivorCount = survivors.Length
    let isLastManStanding = state.Config.PlayerCount > 1 && survivorCount <= 1
    let allBoxesFilled = currentTotalScore = totalBoxes 
    
    let isOver = allBoxesFilled || isLastManStanding || survivorCount = 0

    let winner = 
        if isOver then
            if survivorCount = 1 then 
                Some survivors.Head
            elif survivorCount = 0 then
                None 
            else
                newScores 
                |> Map.toList 
                |> List.filter (fun (p, _) -> not (state.ExplodedPlayers.Contains p))
                |> List.sortByDescending snd
                |> List.tryHead
                |> Option.map fst
        else None

    { state with 
        Scores = newScores
        IsGameOver = isOver
        Winner = winner }

let tryPlaceLine (state: GameState) (linePos: LinePosition) : GameState =
    if state.IsGameOver || state.Lines.ContainsKey linePos then
        state
    else
        let newLines = Map.add linePos state.CurrentTurn state.Lines
        let newBoxesList = findNewlyCompletedBoxes newLines linePos state.Config.Rows state.Config.Cols
        let boxesCompleted = not newBoxesList.IsEmpty
        let triggeredMines = newBoxesList |> List.exists (fun p -> state.Mines.Contains p)
            
        let mutable currentExploded = state.ExplodedPlayers
        if triggeredMines then
            currentExploded <- currentExploded.Add state.CurrentTurn
        
        let newBoxes = 
            newBoxesList 
            |> List.fold (fun acc boxPos -> Map.add boxPos state.CurrentTurn acc) state.Boxes

        let nextPlayer = 
            if triggeredMines then 
                getNextPlayer state.CurrentTurn state.Config.PlayerCount currentExploded
            elif boxesCompleted then 
                state.CurrentTurn 
            else 
                getNextPlayer state.CurrentTurn state.Config.PlayerCount currentExploded

        let newState = { state with 
                            Lines = newLines
                            Boxes = newBoxes
                            ExplodedPlayers = currentExploded
                            CurrentTurn = nextPlayer }
        
        updateGameStatus newState

// ==========================================================================================
// 3. APP MODEL & UPDATE
// ==========================================================================================

type Model = {
    Screen: Screen
    BlinkState: bool
}

type Msg =
    | GoToSettings of GameConfig
    | GoToInGame of GameState
    | GoToColorPicker of int * Color
    | UpdateConfig of GameConfig
    | StartGame 
    | RestartGame
    | PlaceLine of LinePosition
    | BlinkTick
    | SetColor of int
    | IncRows | DecRows
    | IncCols | DecCols
    | IncPlayers | DecPlayers
    | IncMines | DecMines
    | UpdateColorR of byte
    | UpdateColorG of byte
    | UpdateColorB of byte

let init () =
    let baseConfig = {
        Rows = 3
        Cols = 3
        PlayerCount = 2
        MineCount = 1
        PlayerColors = Array.copy availableColors
        LineThickness = 5.0
        DotRadius = 8.0
        Spacing = 60.0
    }
    { 
        Screen = Settings baseConfig
        BlinkState = false 
    }, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    let updateSettings f =
        match model.Screen with
        | Settings config -> { model with Screen = Settings (f config) }, Cmd.none
        | _ -> model, Cmd.none

    let updatePickerColor f =
        match model.Screen with
        | ColorPicker (pid, c, conf) -> 
            { model with Screen = ColorPicker (pid, f c, conf) }, Cmd.none
        | _ -> model, Cmd.none

    match msg with
    | GoToSettings config ->
        { model with Screen = Settings config }, Cmd.none
        
    | GoToInGame gameState ->
        { model with Screen = InGame gameState }, Cmd.none
        
    | GoToColorPicker (pid, color) ->
        match model.Screen with
        | Settings config -> 
            { model with Screen = ColorPicker(pid, color, config) }, Cmd.none
        | ColorPicker (_, _, config) ->
            { model with Screen = ColorPicker(pid, color, config) }, Cmd.none
        | _ -> model, Cmd.none
        
    | IncRows -> updateSettings (fun c -> { c with Rows = c.Rows + 1 })
    | DecRows -> updateSettings (fun c -> { c with Rows = Math.Max(2, c.Rows - 1) })
    | IncCols -> updateSettings (fun c -> { c with Cols = c.Cols + 1 })
    | DecCols -> updateSettings (fun c -> { c with Cols = Math.Max(2, c.Cols - 1) })
    | IncPlayers -> updateSettings (fun c -> { c with PlayerCount = Math.Min(8, c.PlayerCount + 1) })
    | DecPlayers -> updateSettings (fun c -> { c with PlayerCount = Math.Max(2, c.PlayerCount - 1) })
    | IncMines -> updateSettings (fun c -> { c with MineCount = c.MineCount + 1 })
    | DecMines -> updateSettings (fun c -> { c with MineCount = Math.Max(0, c.MineCount - 1) })

    | UpdateConfig config -> { model with Screen = Settings config }, Cmd.none
    
    | UpdateColorR r -> updatePickerColor (fun c -> Color.FromRgb(r, c.G, c.B))
    | UpdateColorG g -> updatePickerColor (fun c -> Color.FromRgb(c.R, g, c.B))
    | UpdateColorB b -> updatePickerColor (fun c -> Color.FromRgb(c.R, c.G, b))

    | StartGame ->
        match model.Screen with
        | Settings config ->
            { model with Screen = InGame (initGame config) }, Cmd.ofEffect (fun dispatch ->
                async {
                    do! Async.Sleep 100
                    dispatch BlinkTick
                } |> Async.StartImmediate
            )
        | _ -> model, Cmd.none

    | RestartGame ->
        match model.Screen with
        | InGame gameState ->
            { model with Screen = InGame (initGame gameState.Config) }, Cmd.ofEffect (fun dispatch ->
                async {
                    do! Async.Sleep 100
                    dispatch BlinkTick
                } |> Async.StartImmediate
            )
        | _ -> model, Cmd.none
        
    | PlaceLine linePos ->
        match model.Screen with
        | InGame gameState ->
            let nextState = tryPlaceLine gameState linePos
            { model with Screen = InGame nextState }, Cmd.none
        | _ -> model, Cmd.none
        
    | BlinkTick ->
        let newModel = { model with BlinkState = not model.BlinkState }
        let shouldContinue = 
            match newModel.Screen with
            | InGame state -> not state.IsGameOver
            | _ -> false
            
        if shouldContinue then
            newModel, Cmd.ofEffect (fun dispatch ->
                async {
                    do! Async.Sleep 100
                    dispatch BlinkTick
                } |> Async.StartImmediate
            )
        else
            newModel, Cmd.none
        
    | SetColor pid ->
        match model.Screen with
        | ColorPicker (_, currentColor, config) ->
            let newColors = Array.copy config.PlayerColors
            newColors.[pid] <- currentColor
            let newConfig = { config with PlayerColors = newColors }
            { model with Screen = Settings newConfig }, Cmd.none
        | _ -> model, Cmd.none

// ==========================================================================================
// 4. VIEW
// ==========================================================================================

let view (model: Model) (dispatch: Msg -> unit) =
    let getPlayerBrush (colors: Color[]) (pid: int) =
        new SolidColorBrush(colors.[pid])

    let settingsView (config: GameConfig) =
        StackPanel.create [
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.children [
                TextBlock.create [
                    TextBlock.text "SETTINGS"
                    TextBlock.fontSize 24.0
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    TextBlock.margin 20.0
                ]
                
                // --- ROWS ---
                Grid.create [
                    Grid.height 40.0
                    Grid.columnDefinitions "100, 50, 50, 50"
                    Grid.margin 5.0
                    Grid.children [
                        TextBlock.create [ Grid.column 0; TextBlock.text "ROWS"; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 1; Button.width 35.0; Button.height 35.0; Button.content "-"
                            Button.onClick (fun _ -> dispatch DecRows)
                        ]
                        TextBlock.create [ Grid.column 2; TextBlock.text (string config.Rows); TextBlock.horizontalAlignment HorizontalAlignment.Center; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 3; Button.width 35.0; Button.height 35.0; Button.content "+"
                            Button.onClick (fun _ -> dispatch IncRows)
                        ]
                    ]
                ]

                // --- COLS ---
                Grid.create [
                    Grid.height 40.0
                    Grid.columnDefinitions "100, 50, 50, 50"
                    Grid.margin 5.0
                    Grid.children [
                        TextBlock.create [ Grid.column 0; TextBlock.text "COLS"; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 1; Button.width 35.0; Button.height 35.0; Button.content "-"
                            Button.onClick (fun _ -> dispatch DecCols)
                        ]
                        TextBlock.create [ Grid.column 2; TextBlock.text (string config.Cols); TextBlock.horizontalAlignment HorizontalAlignment.Center; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 3; Button.width 35.0; Button.height 35.0; Button.content "+"
                            Button.onClick (fun _ -> dispatch IncCols)
                        ]
                    ]
                ]

                // --- PLAYERS ---
                Grid.create [
                    Grid.height 40.0
                    Grid.columnDefinitions "100, 50, 50, 50"
                    Grid.margin 5.0
                    Grid.children [
                        TextBlock.create [ Grid.column 0; TextBlock.text "PLAYERS"; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 1; Button.width 35.0; Button.height 35.0; Button.content "-"
                            Button.onClick (fun _ -> dispatch DecPlayers)
                        ]
                        TextBlock.create [ Grid.column 2; TextBlock.text (string config.PlayerCount); TextBlock.horizontalAlignment HorizontalAlignment.Center; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 3; Button.width 35.0; Button.height 35.0; Button.content "+"
                            Button.onClick (fun _ -> dispatch IncPlayers)
                        ]
                    ]
                ]

                // --- MINES ---
                Grid.create [
                    Grid.height 40.0
                    Grid.columnDefinitions "100, 50, 50, 50"
                    Grid.margin 5.0
                    Grid.children [
                        TextBlock.create [ Grid.column 0; TextBlock.text "MINES"; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 1; Button.width 35.0; Button.height 35.0; Button.content "-"
                            Button.onClick (fun _ -> dispatch DecMines)
                        ]
                        TextBlock.create [ Grid.column 2; TextBlock.text (string config.MineCount); TextBlock.horizontalAlignment HorizontalAlignment.Center; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.fontSize 16.0 ]
                        Button.create [
                            Grid.column 3; Button.width 35.0; Button.height 35.0; Button.content "+"
                            Button.onClick (fun _ -> dispatch IncMines)
                        ]
                    ]
                ]

                TextBlock.create [
                    TextBlock.text "PLAYER COLORS"
                    TextBlock.margin (0.0, 20.0, 0.0, 10.0)
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                ]
                
                WrapPanel.create [
                    WrapPanel.horizontalAlignment HorizontalAlignment.Center
                    WrapPanel.maxWidth 300.0
                    WrapPanel.children [
                        for i in 0 .. config.PlayerCount - 1 do
                            yield Button.create [
                                Button.width 40.0
                                Button.height 40.0
                                Button.margin 5.0
                                Button.background (new SolidColorBrush(config.PlayerColors.[i]))
                                Button.content (sprintf "P%d" (i+1))
                                Button.foreground (new SolidColorBrush(Colors.White))
                                Button.onClick (fun _ -> dispatch (GoToColorPicker(i, config.PlayerColors.[i])))
                            ] :> IView
                    ]
                ]

                Button.create [
                    Button.content "START GAME"
                    Button.fontSize 20.0
                    Button.background (new SolidColorBrush(Colors.LightGreen))
                    Button.margin (0.0, 30.0, 0.0, 0.0)
                    Button.horizontalAlignment HorizontalAlignment.Center
                    Button.padding (20.0, 10.0)
                    Button.onClick (fun _ -> dispatch StartGame)
                ]
            ]
        ]

    let colorPickerView (targetPid: int) (color: Color) (returnConfig: GameConfig) =
        StackPanel.create [
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.children [
                TextBlock.create [
                    TextBlock.text (sprintf "PICK COLOR FOR P%d" (targetPid + 1))
                    TextBlock.fontSize 20.0
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    TextBlock.margin 20.0
                ]
                
                Border.create [
                    Border.width 100.0
                    Border.height 50.0
                    Border.background (new SolidColorBrush(color))
                    Border.borderBrush (new SolidColorBrush(Colors.Black))
                    Border.borderThickness 2.0
                    Border.horizontalAlignment HorizontalAlignment.Center
                    Border.margin 10.0
                ]

                // --- R Slider ---
                StackPanel.create [
                    StackPanel.margin 5.0
                    StackPanel.children [
                        TextBlock.create [ TextBlock.text (sprintf "R: %d" color.R) ]
                        Slider.create [
                            Slider.minimum 0.0
                            Slider.maximum 255.0
                            Slider.value (float color.R)
                            Slider.onValueChanged (fun v -> dispatch (UpdateColorR (byte v)))
                            Slider.width 200.0
                        ]
                    ]
                ]

                // --- G Slider ---
                StackPanel.create [
                    StackPanel.margin 5.0
                    StackPanel.children [
                        TextBlock.create [ TextBlock.text (sprintf "G: %d" color.G) ]
                        Slider.create [
                            Slider.minimum 0.0
                            Slider.maximum 255.0
                            Slider.value (float color.G)
                            Slider.onValueChanged (fun v -> dispatch (UpdateColorG (byte v)))
                            Slider.width 200.0
                        ]
                    ]
                ]

                // --- B Slider ---
                StackPanel.create [
                    StackPanel.margin 5.0
                    StackPanel.children [
                        TextBlock.create [ TextBlock.text (sprintf "B: %d" color.B) ]
                        Slider.create [
                            Slider.minimum 0.0
                            Slider.maximum 255.0
                            Slider.value (float color.B)
                            Slider.onValueChanged (fun v -> dispatch (UpdateColorB (byte v)))
                            Slider.width 200.0
                        ]
                    ]
                ]

                StackPanel.create [
                    StackPanel.orientation Avalonia.Layout.Orientation.Horizontal
                    StackPanel.horizontalAlignment HorizontalAlignment.Center
                    StackPanel.margin 20.0
                    StackPanel.children [
                        Button.create [
                            Button.content "OK"
                            Button.width 80.0
                            Button.background (new SolidColorBrush(Colors.LightGreen))
                            Button.margin 10.0
                            Button.onClick (fun _ -> dispatch (SetColor targetPid))
                        ]
                        Button.create [
                            Button.content "BACK"
                            Button.width 80.0
                            Button.onClick (fun _ -> dispatch (GoToSettings returnConfig))
                        ]
                    ]
                ]
            ]
        ]

    let inGameView (gameState: GameState) =
        let config = gameState.Config
        let spacing = config.Spacing
        let dotRadius = config.DotRadius
        let lineThick = config.LineThickness
        let gridWidth = float config.Cols * spacing
        let gridHeight = float config.Rows * spacing
        
        let findClickedLine (mousePos: Point) =
            let relX = mousePos.X
            let relY = mousePos.Y
            let threshold = 15.0
            let mutable bestLine = None
            let mutable minDist = Double.MaxValue
            
            for r in 0 .. config.Rows do
                for c in 0 .. config.Cols - 1 do
                    let cx = float c * spacing + spacing / 2.0
                    let cy = float r * spacing
                    if relX >= (float c * spacing) && relX <= (float (c+1) * spacing) then
                        let dist = Math.Abs(relY - cy)
                        if dist < threshold && dist < minDist then
                            minDist <- dist
                            bestLine <- Some { Pos = { Row = r; Col = c }; Orient = GameOrientation.Horizontal }

            for r in 0 .. config.Rows - 1 do
                for c in 0 .. config.Cols do
                    let cx = float c * spacing
                    let cy = float r * spacing + spacing / 2.0
                    if relY >= (float r * spacing) && relY <= (float (r+1) * spacing) then
                        let dist = Math.Abs(relX - cx)
                        if dist < threshold && dist < minDist then
                            minDist <- dist
                            bestLine <- Some { Pos = { Row = r; Col = c }; Orient = GameOrientation.Vertical }
            bestLine

        let getPotentialBoxes (line: LinePosition) = 
            match line.Orient with
            | GameOrientation.Horizontal -> 
                [ { Row = line.Pos.Row; Col = line.Pos.Col }
                  { Row = line.Pos.Row - 1; Col = line.Pos.Col } ]
            | GameOrientation.Vertical -> 
                [ { Row = line.Pos.Row; Col = line.Pos.Col }
                  { Row = line.Pos.Row; Col = line.Pos.Col - 1 } ]

        let canvasChildren = 
            [
                for box in gameState.Boxes do
                    let pos = box.Key
                    let owner = box.Value
                    let color = config.PlayerColors.[owner]
                    let x = float pos.Col * spacing + 5.0
                    let y = float pos.Row * spacing + 5.0
                    
                    if gameState.Mines.Contains pos then
                        yield Border.create [
                            Canvas.left x
                            Canvas.top y
                            Border.width (spacing - 10.0)
                            Border.height (spacing - 10.0)
                            Border.background (new SolidColorBrush(Colors.DarkGray))
                            Border.child (
                                TextBlock.create [
                                    TextBlock.text "☠"
                                    TextBlock.foreground (new SolidColorBrush(Colors.Red))
                                    TextBlock.fontSize 24.0
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                    TextBlock.verticalAlignment VerticalAlignment.Center
                                ]
                            )
                        ] :> IView
                    else
                        yield Rectangle.create [
                            Canvas.left x
                            Canvas.top y
                            Rectangle.width (spacing - 10.0)
                            Rectangle.height (spacing - 10.0)
                            Rectangle.fill (new SolidColorBrush(color, 0.5))
                        ] :> IView

                for line in gameState.Lines do
                    let pos = line.Key
                    let color = config.PlayerColors.[line.Value]
                    let x = float pos.Pos.Col * spacing
                    let y = float pos.Pos.Row * spacing
                    
                    let nearMine = 
                        getPotentialBoxes pos 
                        |> List.exists (fun boxPos -> 
                            gameState.Mines.Contains boxPos && 
                            not (gameState.Boxes.ContainsKey boxPos)) 
                    
                    let brush = 
                        if nearMine then
                            if model.BlinkState then new SolidColorBrush(Colors.Red) else new SolidColorBrush(Colors.Yellow)
                        else
                            new SolidColorBrush(color)

                    if pos.Orient = GameOrientation.Horizontal then
                        yield Rectangle.create [
                            Canvas.left x
                            Canvas.top (y - lineThick/2.0)
                            Rectangle.width spacing
                            Rectangle.height lineThick
                            Rectangle.fill brush
                        ] :> IView
                    else
                        yield Rectangle.create [
                            Canvas.left (x - lineThick/2.0)
                            Canvas.top y
                            Rectangle.width lineThick
                            Rectangle.height spacing
                            Rectangle.fill brush
                        ] :> IView

                for r in 0 .. config.Rows do
                    for c in 0 .. config.Cols do
                        let x = float c * spacing
                        let y = float r * spacing
                        yield Ellipse.create [
                            Canvas.left (x - dotRadius)
                            Canvas.top (y - dotRadius)
                            Ellipse.width (dotRadius * 2.0)
                            Ellipse.height (dotRadius * 2.0)
                            Ellipse.fill (new SolidColorBrush(Colors.Black))
                        ] :> IView
            ]

        DockPanel.create [
            DockPanel.children [
                Grid.create [
                    DockPanel.dock Dock.Top
                    Grid.margin 10.0
                    Grid.columnDefinitions "*, Auto"
                    Grid.children [
                        StackPanel.create [
                            Grid.column 0
                            StackPanel.orientation Avalonia.Layout.Orientation.Horizontal
                            StackPanel.children [
                                for p in 0 .. config.PlayerCount - 1 do
                                    let score = gameState.Scores.[p]
                                    let status = if gameState.ExplodedPlayers.Contains p then "DEAD" else string score
                                    yield TextBlock.create [
                                        TextBlock.text (sprintf "P%d: %s" (p+1) status)
                                        TextBlock.foreground (getPlayerBrush config.PlayerColors p)
                                        TextBlock.fontWeight FontWeight.Bold
                                        TextBlock.margin (0.0, 0.0, 15.0, 0.0)
                                    ] :> IView
                            ]
                        ]
                        StackPanel.create [
                            Grid.column 1
                            StackPanel.orientation Avalonia.Layout.Orientation.Horizontal
                            StackPanel.children [
                                if not gameState.IsGameOver then
                                    TextBlock.create [
                                        TextBlock.text (sprintf "TURN: P%d" (gameState.CurrentTurn + 1))
                                        TextBlock.foreground (getPlayerBrush config.PlayerColors gameState.CurrentTurn)
                                        TextBlock.fontWeight FontWeight.Bold
                                        TextBlock.margin (0.0, 0.0, 20.0, 0.0)
                                    ]
                                Button.create [
                                    Button.content "BACK"
                                    Button.onClick (fun _ -> dispatch (GoToSettings config))
                                ]
                            ]
                        ]
                    ]
                ]

                ScrollViewer.create [
                    ScrollViewer.horizontalScrollBarVisibility ScrollBarVisibility.Auto
                    ScrollViewer.verticalScrollBarVisibility ScrollBarVisibility.Auto
                    ScrollViewer.content (
                        Grid.create [
                            Grid.horizontalAlignment HorizontalAlignment.Center
                            Grid.verticalAlignment VerticalAlignment.Center
                            Grid.children [
                                Canvas.create [
                                    Canvas.width (gridWidth + 20.0)
                                    Canvas.height (gridHeight + 20.0)
                                    Canvas.background (new SolidColorBrush(Colors.Transparent))
                                    Canvas.children canvasChildren
                                    Canvas.onPointerPressed (fun e ->
                                        let pos = e.GetPosition(e.Source :?> Visual)
                                        match findClickedLine pos with
                                        | Some line -> dispatch (PlaceLine line)
                                        | None -> ()
                                    )
                                ]
                                
                                if gameState.IsGameOver then
                                    Border.create [
                                        Border.background (new SolidColorBrush(Colors.White, 0.8))
                                        Border.child (
                                            StackPanel.create [
                                                StackPanel.verticalAlignment VerticalAlignment.Center
                                                StackPanel.horizontalAlignment HorizontalAlignment.Center
                                                StackPanel.children [
                                                    TextBlock.create [
                                                        TextBlock.text (
                                                            match gameState.Winner with
                                                            | Some p -> sprintf "P%d WINS!" (p+1)
                                                            | None -> "DRAW"
                                                        )
                                                        TextBlock.fontSize 40.0
                                                        TextBlock.fontWeight FontWeight.Bold
                                                        TextBlock.foreground (
                                                            match gameState.Winner with
                                                            | Some p -> getPlayerBrush config.PlayerColors p
                                                            | None -> (new SolidColorBrush(Colors.Black))
                                                        )
                                                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                                                    ]
                                                    Button.create [
                                                        Button.content "RESTART"
                                                        Button.horizontalAlignment HorizontalAlignment.Center
                                                        Button.margin 20.0
                                                        Button.fontSize 20.0
                                                        Button.onClick (fun _ -> dispatch RestartGame)
                                                    ]
                                                ]
                                            ]
                                        )
                                    ]
                            ]
                        ]
                    )
                ]
            ]
        ]

    match model.Screen with
    | Settings config -> settingsView config :> IView
    | ColorPicker (p, c, conf) -> colorPickerView p c conf :> IView
    | InGame gameState -> inGameView gameState :> IView
    | MainMenu -> TextBlock.create [ TextBlock.text "Menu" ] :> IView

// ==========================================================================================
// 5. APP ENTRY
// ==========================================================================================

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Mine Dots and Boxes (Avalonia)"
        base.Width <- 800.0
        base.Height <- 800.0
        
        ElmishProgram.mkProgram init update view
        |> Avalonia.FuncUI.Elmish.Program.withHost this
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
