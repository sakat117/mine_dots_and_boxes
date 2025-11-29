module MineDotsAndBoxesAvalonia.GameLogic

open MineDotsAndBoxesAvalonia.Domain
open System

// --- Helpers ---

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
