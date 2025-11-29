module MineDotsAndBoxesFs.GameLogic

open MineDotsAndBoxesFs.Domain
open Microsoft.Xna.Framework

// --- Helpers ---

let getNextPlayer (current: GamePlayerIndex) (totalPlayers: int) =
    (current + 1) % totalPlayers

// Initialize a new game
let initGame (config: GameConfig) : GameState =
    // Initialize scores for all players to 0
    let initialScores = 
        [0 .. config.PlayerCount - 1] 
        |> List.map (fun p -> (p, 0)) 
        |> Map.ofList

    {
        Config = config
        Lines = Map.empty
        Boxes = Map.empty
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
    // Note: Grid of boxes is size Rows x Cols. 
    // The dots grid is (Rows+1) x (Cols+1).
    potentialBoxes
    |> List.filter (fun p -> p.Row >= 0 && p.Row < rows && p.Col >= 0 && p.Col < cols)
    |> List.filter (fun p -> isBoxCompleted lines p)


// Update scores and game over status
let updateGameStatus (state: GameState) : GameState =
    // Recalculate scores
    let newScores = 
        [0 .. state.Config.PlayerCount - 1]
        |> List.map (fun pid -> 
            let score = state.Boxes |> Map.filter (fun _ owner -> owner = pid) |> Map.count
            (pid, score))
        |> Map.ofList
    
    let totalBoxes = state.Config.Rows * state.Config.Cols
    let currentTotalScore = newScores |> Map.fold (fun acc _ s -> acc + s) 0
    
    let isOver = currentTotalScore = totalBoxes
    
    let winner = 
        if isOver then
            // Find player with max score
            newScores 
            |> Map.toList 
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
        
        // 3. Update ownership of new boxes
        let newBoxes = 
            newBoxesList 
            |> List.fold (fun acc boxPos -> Map.add boxPos state.CurrentTurn acc) state.Boxes

        // 4. Determine next turn (same player if box completed, else next)
        let nextPlayer = 
            if boxesCompleted then state.CurrentTurn 
            else getNextPlayer state.CurrentTurn state.Config.PlayerCount

        let newState = { state with 
                            Lines = newLines
                            Boxes = newBoxes
                            CurrentTurn = nextPlayer }
        
        // 5. Update scores and check game over
        updateGameStatus newState