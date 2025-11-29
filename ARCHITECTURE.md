# アーキテクチャガイド

このドキュメントでは、**Mine Dots and Boxes** プロジェクトの技術的な構成について解説します。

## 概要

このアプリケーションは、F# と Avalonia.FuncUI を使用した **シングルファイル・アプリケーション** です。
**Elmish (MVU)** アーキテクチャを採用しており、状態（State）、ロジック（Logic）、表示（View）が明確に分離されています。

### MVU データフロー

```mermaid
graph LR
    User(("User / Timer")) -->|Msg| Update
    subgraph "Elmish Loop"
        Update -- "Change State" --> Model
        Model -->|State| View
        View -->|Dispatch| Update
    end
    Update -->|Cmd| Effect[Side Effects]
    Effect -->|"Async Msg"| Update
    View -->|Render| Screen["UI Window"]
```

## ファイル構成

すべてのコードは `MineDotsAndBoxesAvalonia/Program.fs` に集約されています。
ファイル内は以下のセクション順に構成されています。

1.  **Imports & Aliases**: 必要な名前空間とエイリアス定義。
2.  **Domain**: ゲームの核となる型定義。
3.  **Game Logic**: ゲームのルールを記述した純粋関数群。
4.  **App Model & Update**: アプリケーション全体の状態定義とメッセージ処理。
5.  **View**: Avalonia.FuncUI DSL を使ったUI定義。
6.  **App Entry**: `MainWindow` クラスとエントリーポイント。

## データ構造 (Domain)

主要なデータ型とその関係性は以下の通りです。

```mermaid
classDiagram
    class Model {
        +Screen CurrentScreen
        +bool BlinkState
    }

    class Screen {
        <<Union>>
        +Settings(GameConfig)
        +InGame(GameState)
        +ColorPicker
    }

    class GameConfig {
        +int Rows
        +int Cols
        +int PlayerCount
        +int MineCount
        +Color[] PlayerColors
    }

    class GameState {
        +Map Lines
        +Map Boxes
        +Set Mines
        +Set ExplodedPlayers
        +int CurrentTurn
        +Map Scores
        +bool IsGameOver
    }

    Model --> Screen
    Screen --> GameConfig : holds
    Screen --> GameState : holds
    GameState --> GameConfig : reference
```

## アプリケーション遷移 (State Machine)

アプリケーションの画面遷移図です。

```mermaid
stateDiagram-v2
    [*] --> Settings : Launch
    
    state Settings {
        [*] --> ConfigView
        ConfigView --> ConfigView : Inc/Dec Parameters
    }

    state ColorPicker {
        [*] --> Editing
        Editing --> Editing : Slider Change
    }

    state InGame {
        [*] --> Playing
        Playing --> Playing : Place Line / Blink Tick
        Playing --> GameOver : Condition Met
        GameOver --> Playing : Restart
    }

    Settings --> ColorPicker : GoToColorPicker
    ColorPicker --> Settings : OK / Back
    Settings --> InGame : StartGame
    InGame --> Settings : Back (ESC)
```

## ゲームロジック (Game Flow)

線を引いた際の判定ロジック（`tryPlaceLine` 関数）の流れです。

```mermaid
flowchart TD
    Start([Player Clicks Line]) --> Check{"Valid Move?"}
    Check -- "No (Existing Line)" --> Ignore([Ignore])
    Check -- Yes --> UpdateLines[Update Lines Map]
    UpdateLines --> BoxCheck{"Box Completed?"}
    
    BoxCheck -- No --> NextTurn[Next Player]
    
    BoxCheck -- Yes --> MineCheck{"Is Mine?"}
    
    MineCheck -- "Yes (BOOM)" --> Explode[Add to ExplodedPlayers]
    Explode --> CheckSurvivors{"Survivors > 1?"}
    CheckSurvivors -- Yes --> NextTurnMine["Next Survivor's Turn"]
    CheckSurvivors -- No --> GameEnd([Game Over])
    
    MineCheck -- "No (Safe)" --> Score[Update Score]
    Score --> GameEndCheck{"All Boxes Filled?"}
    GameEndCheck -- Yes --> GameEnd
    GameEndCheck -- No --> SameTurn["Same Player's Turn"]
```

## ヒットテスト（クリック判定）

標準的なボタン部品を使わず、独自の計算で判定を行っています。

```mermaid
flowchart LR
    Click((Mouse Click)) --> GetPos["Get Coordinates (X, Y)"]
    GetPos --> Loop[Loop All Potential Lines]
    Loop --> Calc[Calculate Distance to Line Center]
    Calc --> Threshold{"Distance < 15px?"}
    Threshold -- Yes --> Hit[Line Selected]
    Threshold -- No --> Continue[Check Next Line]
```

## 警告システム（点滅）

マインスイーパー要素である「警告」の実装です。

1.  **判定**: `View` 関数内で、描画しようとしている線の「隣接するボックス」を取得します。
2.  **条件**: 「隣接ボックスに爆弾がある」かつ「そのボックスが未獲得」の場合、警告フラグが立ちます。
3.  **描画**: `BlinkState`（タイマーで0.1秒ごとに反転）に基づき、線の色を赤/黄色に切り替えます。

## 今後の改善案
*   **効果音**: 線を引いたとき、爆発したとき、勝利したときに音を鳴らす。
*   **AI対戦**: 一人プレイ用に、ミニマックス法などを使ったCPU対戦相手の実装。