module MineDotsAndBoxesAvalonia.Domain

open Avalonia.Media // Use Avalonia color

// --- Core Game Elements ---

// Player is now represented by an index (0 to PlayerCount - 1)
type GamePlayerIndex = int 

type GameOrientation = 
    | Horizontal 
    | Vertical

type Position = { Row: int; Col: int }

// A line is defined by its grid position and orientation
type LinePosition = { Pos: Position; Orient: GameOrientation }

type BoxOwner = GamePlayerIndex

// --- Game State ---

type GameConfig = {
    Rows: int
    Cols: int
    PlayerCount: int
    MineCount: int
    PlayerColors: Color[]
    // Visual settings
    LineThickness: double
    DotRadius: double
    Spacing: double
}

type GameState = {
    Config: GameConfig
    Lines: Map<LinePosition, GamePlayerIndex> // Tracks drawn lines and who drew them
    Boxes: Map<Position, GamePlayerIndex>     // Tracks completed boxes and who owns them
    Mines: Set<Position>                      // Positions of mines
    ExplodedPlayers: Set<GamePlayerIndex>     // Players who triggered a mine
    CurrentTurn: GamePlayerIndex
    Scores: Map<GamePlayerIndex, int>
    IsGameOver: bool
    Winner: GamePlayerIndex option
}

// --- Application State (Screen Management) ---

type Screen =
    | MainMenu
    | InGame of GameState
    | Settings of GameConfig
    // ColorPicker state needs to hold which player we are editing and the temp color
    | ColorPicker of targetPlayer: int * editingColor: Color * returnConfig: GameConfig
