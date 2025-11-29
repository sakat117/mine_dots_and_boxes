module MineDotsAndBoxesFs.GameLogic

open MineDotsAndBoxesFs.Domain
open System

// --- Helpers ---

// Get next player, skipping exploded ones
let getNextPlayer (current: GamePlayerIndex) (totalPlayers: int) (exploded: Set<GamePlayerIndex>) =
    let rec findNext p =
        let next = (p + 1) % totalPlayers
        if next = current then None // Should not happen unless everyone exploded?
        elif exploded.Contains next then findNext next
        else Some next
    
    // If everyone else exploded, we might just stay current or handle game over elsewhere
    // Here we just assume there is at least one survivor or game ends
    match findNext current with
    | Some p -> p
    | None -> current 

// Initialize a new game
let initGame (config: GameConfig) : GameState =
    // Initialize scores for all players to 0
    let initialScores = 
        [0 .. config.PlayerCount - 1] 
        |> List.map (fun p -> (p, 0)) 
        |> Map.ofList

    // Place Mines Randomly
    let rnd = Random()
    let totalBoxes = config.Rows * config.Cols
    // Generate all positions
    let allPositions = 
        [ for r in 0 .. config.Rows - 1 do
            for c in 0 .. config.Cols - 1 do
                yield { Row = r; Col = c } ]
    
    // Shuffle and take MineCount
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
        CurrentTurn = 0 // Player 0 starts
        Scores = initialScores
        IsGameOver = false
        Winner = None
    }

// Get the 4 lines that surround a specific box position
let getBoxEdges (pos: Position) : LinePosition list =
    [
        { Pos = pos; Orient = Horizontal } // Top
        { Pos = { pos with Row = pos.Row + 1 }; Orient = Horizontal } // Bottom
        { Pos = pos; Orient = Vertical } // Left
        { Pos = { pos with Col = pos.Col + 1 }; Orient = Vertical } // Right
    ]

// Check if a box at 'pos' is completed (surrounded by lines)
let isBoxCompleted (lines: Map<LinePosition, GamePlayerIndex>) (pos: Position) : bool =
    let edges = getBoxEdges pos
    edges |> List.forall (fun edge -> lines.ContainsKey edge)

// Check all potential boxes that might have been completed by placing a specific line
let findNewlyCompletedBoxes (lines: Map<LinePosition, GamePlayerIndex>) (newLine: LinePosition) (rows: int) (cols: int) : Position list =
    let potentialBoxes = 
        match newLine.Orient with
        | Horizontal -> 
            [ 
                { Row = newLine.Pos.Row; Col = newLine.Pos.Col }     // Box below the line
                { Row = newLine.Pos.Row - 1; Col = newLine.Pos.Col } // Box above the line
            ]
        | Vertical -> 
            [ 
                { Row = newLine.Pos.Row; Col = newLine.Pos.Col }     // Box to the right
                { Row = newLine.Pos.Row; Col = newLine.Pos.Col - 1 } // Box to the left
            ]
    
    // Filter boxes that are within grid bounds and are now fully completed
    potentialBoxes
    |> List.filter (fun p -> p.Row >= 0 && p.Row < rows && p.Col >= 0 && p.Col < cols)
    |> List.filter (fun p -> isBoxCompleted lines p)


// Update scores and game over status
let updateGameStatus (state: GameState) : GameState =
    // Recalculate scores (only for survivors?)
    // Survivors get points for boxes they own. Exploded players keep 0 or stay as is.
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
    
    // Game Over conditions:
    // 1. All boxes filled
    // 2. Only 1 survivor left (if started with > 1)
    // 3. All players exploded (draw?)
    
    let survivorCount = survivors.Length
    let isLastManStanding = state.Config.PlayerCount > 1 && survivorCount <= 1
    let allBoxesFilled = currentTotalScore = totalBoxes // Note: boxes owned by exploded players still count as filled?
    // Actually if a player explodes, do they "own" the box? 
    // Current logic assigns box to player BEFORE checking explosion. 
    // So yes, box is filled.
    
    // However, if a player explodes, maybe we want the game to end immediately if they were the last opponent?
    
    let isOver = allBoxesFilled || isLastManStanding || survivorCount = 0

    let winner = 
        if isOver then
            if survivorCount = 1 then 
                Some survivors.Head
            elif survivorCount = 0 then
                None // Everyone died
            else
                // Score based win
                newScores 
                |> Map.toList 
                |> List.filter (fun (p, _) -> not (state.ExplodedPlayers.Contains p)) // Only survivors can win?
                |> List.sortByDescending snd
                |> List.tryHead
                |> Option.map fst
        else None

    { state with 
        Scores = newScores
        IsGameOver = isOver
        Winner = winner }


// Attempt to place a line. Returns the new state.
let tryPlaceLine (state: GameState) (linePos: LinePosition) : GameState =
    if state.IsGameOver || state.Lines.ContainsKey linePos then
        state
    else
        // 1. Place the line
        let newLines = Map.add linePos state.CurrentTurn state.Lines
        
        // 2. Check for completed boxes
        let newBoxesList = findNewlyCompletedBoxes newLines linePos state.Config.Rows state.Config.Cols
        
        let boxesCompleted = not newBoxesList.IsEmpty
        
        // 3. Check for Mines in newly completed boxes
        let triggeredMines = 
            newBoxesList 
            |> List.exists (fun p -> state.Mines.Contains p)
            
        let mutable currentExploded = state.ExplodedPlayers
        if triggeredMines then
            currentExploded <- currentExploded.Add state.CurrentTurn
        
        // 4. Update ownership of new boxes
        // Even if exploded, assign box to them for now (to mark it as filled)
        let newBoxes = 
            newBoxesList 
            |> List.fold (fun acc boxPos -> Map.add boxPos state.CurrentTurn acc) state.Boxes

        // 5. Determine next turn 
        // - If player exploded, force next player.
        // - If box completed and NOT exploded, same player.
        // - Else next player.
        
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
        
        // 6. Update scores and check game over
        updateGameStatus newState
