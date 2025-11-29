module MineDotsAndBoxesFs.Domain

open Microsoft.Xna.Framework // For Color

// --- Core Game Elements ---

// Player is now represented by an index (0 to PlayerCount - 1)
type GamePlayerIndex = int 

type Orientation = 
    | Horizontal 
    | Vertical

type Position = { Row: int; Col: int }

// A line is defined by its grid position and orientation
type LinePosition = { Pos: Position; Orient: Orientation }

type BoxOwner = GamePlayerIndex

// --- Game State ---

type GameConfig = {
    Rows: int
    Cols: int
    PlayerCount: int
    PlayerColors: Color[]
    // Visual settings
    LineThickness: int
    DotRadius: int
    Spacing: int
}

type GameState = {
    Config: GameConfig
    Lines: Map<LinePosition, GamePlayerIndex> // Tracks drawn lines and who drew them
    Boxes: Map<Position, GamePlayerIndex>     // Tracks completed boxes and who owns them
    CurrentTurn: GamePlayerIndex
    Scores: Map<GamePlayerIndex, int>
    IsGameOver: bool
    Winner: GamePlayerIndex option // In case of a tie, handle display logic separately
}

// --- Application State (Screen Management) ---

type Screen =
    | MainMenu
    | InGame of GameState
    | Settings of GameConfig
    | ColorPicker of targetPlayer: int * editingColor: Color * returnConfig: GameConfig