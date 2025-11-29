module MineDotsAndBoxesFs.Program

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open MineDotsAndBoxesFs.Domain
open MineDotsAndBoxesFs.GameLogic
open MineDotsAndBoxesFs.VectorGraphics

type DotsAndBoxesGame() as this =
    inherit Game()
    
    let graphics = new GraphicsDeviceManager(this)
    let mutable spriteBatch : SpriteBatch = null
    let mutable whitePixel : Texture2D = null
    
    // --- Color Palette ---
    let availableColors = [|
        Color.Blue; Color.Red; Color.Green; Color.Orange;
        Color.Purple; Color.Cyan; Color.Yellow; Color.Black;
        Color.Magenta; Color.Lime; Color.Brown; Color.Teal;
        Color.Pink; Color.Gray
    |]
    
    // --- Global Constants ---
    let baseConfig = {
        Rows = 3
        Cols = 3
        PlayerCount = 2
        PlayerColors = Array.copy availableColors // Initialize with default palette
        LineThickness = 5
        DotRadius = 8
        Spacing = 60
    }

    // --- Application State ---
    let mutable currentScreen : Screen = Settings baseConfig
    
    // --- Input State ---
    let mutable prevMouseState = Mouse.GetState()
    let mutable prevKeyboardState = Keyboard.GetState()

    override this.Initialize() =
        graphics.PreferredBackBufferWidth <- 800
        graphics.PreferredBackBufferHeight <- 800
        graphics.ApplyChanges()
        this.IsMouseVisible <- true
        base.Initialize()

    override this.LoadContent() =
        spriteBatch <- new SpriteBatch(this.GraphicsDevice)
        whitePixel <- new Texture2D(this.GraphicsDevice, 1, 1)
        whitePixel.SetData([| Color.White |])

    // --- Helper: UI Hit Test ---
    member this.IsClicked(rect: Rectangle, mouse: MouseState) =
        rect.Contains(mouse.X, mouse.Y) && 
        mouse.LeftButton = ButtonState.Pressed && 
        prevMouseState.LeftButton = ButtonState.Released

    // Helper check if mouse is currently held down over a rect (or was clicked in it)
    // For sliders, we just check if button is down and within Y range roughly
    member this.IsDragging(rect: Rectangle, mouse: MouseState) =
        // Relaxed hit test for sliders: check if mouse is within X range extended, and Y range
        let expanded = new Rectangle(rect.X, rect.Y - 10, rect.Width, rect.Height + 20)
        expanded.Contains(mouse.X, mouse.Y) && mouse.LeftButton = ButtonState.Pressed

    // --- Update Logic ---

    member this.UpdateSettings(config: GameConfig, gameTime: GameTime) =
        let mouse = Mouse.GetState()
        let viewport = this.GraphicsDevice.Viewport
        let cX = viewport.Width / 2
        let startY = viewport.Height / 2 - 150
        
        let getRowY index = startY + index * 70
        let getMinusRect y = new Rectangle(cX - 150, y, 60, 60)
        let getPlusRect y = new Rectangle(cX + 90, y, 60, 60)
        
        // 1. Rows Control
        let rowY = getRowY 0
        if this.IsClicked(getMinusRect rowY, mouse) then
            currentScreen <- Settings { config with Rows = Math.Max(2, config.Rows - 1) }
        if this.IsClicked(getPlusRect rowY, mouse) then
            currentScreen <- Settings { config with Rows = Math.Min(10, config.Rows + 1) }

        // 2. Cols Control
        let colY = getRowY 1
        if this.IsClicked(getMinusRect colY, mouse) then
            currentScreen <- Settings { config with Cols = Math.Max(2, config.Cols - 1) }
        if this.IsClicked(getPlusRect colY, mouse) then
            currentScreen <- Settings { config with Cols = Math.Min(10, config.Cols + 1) }

        // 3. Players Control
        let plY = getRowY 2
        if this.IsClicked(getMinusRect plY, mouse) then
            currentScreen <- Settings { config with PlayerCount = Math.Max(2, config.PlayerCount - 1) }
        if this.IsClicked(getPlusRect plY, mouse) then
            currentScreen <- Settings { config with PlayerCount = Math.Min(8, config.PlayerCount + 1) }

        // 4. Color Pickers
        let colorStartY = getRowY 3 + 20
        let btnSize = 50
        let spacing = 10
        
        for i in 0 .. config.PlayerCount - 1 do
            let row = i / 4
            let col = i % 4
            let itemsInRow = 
                if row < (config.PlayerCount / 4) then 4 
                else config.PlayerCount % 4
            let itemsInRow = if itemsInRow = 0 then 4 else itemsInRow
            
            let rowWidth = itemsInRow * (btnSize + spacing) - spacing
            let startX = cX - rowWidth / 2
            
            let x = startX + col * (btnSize + spacing)
            let y = colorStartY + row * (btnSize + spacing)
            let rect = new Rectangle(x, y, btnSize, btnSize)
            
            if this.IsClicked(rect, mouse) then
                // Go to Color Picker
                currentScreen <- ColorPicker(i, config.PlayerColors.[i], config)
        
        // Play Button
        let playBtnY = colorStartY + (if config.PlayerCount > 4 then 2 else 1) * (btnSize + spacing) + 20
        let playBtn = new Rectangle(cX - 60, playBtnY, 120, 70)
        
        if this.IsClicked(playBtn, mouse) then
            let newGame = initGame config
            currentScreen <- InGame newGame

        this.Window.Title <- "Dots and Boxes | Settings"

    member this.UpdateColorPicker(targetPid: int, color: Color, config: GameConfig) =
        let mouse = Mouse.GetState()
        let viewport = this.GraphicsDevice.Viewport
        let cX = viewport.Width / 2
        let cY = viewport.Height / 2
        
        // Slider Layout
        let sliderWidth = 300
        let sliderHeight = 30
        let sliderX = cX - sliderWidth / 2
        let startY = cY - 50
        let spacing = 60
        
        // Logic to update color component
        let updateComponent (rect: Rectangle) (currentVal: byte) =
            if mouse.LeftButton = ButtonState.Pressed then
                // Check if mouse is within X bounds (with some padding) and Y bounds
                let padding = 10
                if mouse.Y >= rect.Y - padding && mouse.Y <= rect.Y + rect.Height + padding then
                    let relX = float (mouse.X - rect.X)
                    let ratio = Math.Clamp(relX / float rect.Width, 0.0, 1.0)
                    Some (byte (ratio * 255.0))
                else None
            else None

        let rRect = new Rectangle(sliderX, startY, sliderWidth, sliderHeight)
        let gRect = new Rectangle(sliderX, startY + spacing, sliderWidth, sliderHeight)
        let bRect = new Rectangle(sliderX, startY + spacing * 2, sliderWidth, sliderHeight)
        
        // Use mutable to construct new color
        let mutable r, g, b = color.R, color.G, color.B
        
        match updateComponent rRect r with Some v -> r <- v | None -> ()
        match updateComponent gRect g with Some v -> g <- v | None -> ()
        match updateComponent bRect b with Some v -> b <- v | None -> ()
        
        let newColor = Color(int r, int g, int b)
        
        // UI Buttons (RESET Removed)
        let btnY = startY + spacing * 3 + 20
        let okBtn = new Rectangle(cX - 110, btnY, 100, 50)
        let cancelBtn = new Rectangle(cX + 10, btnY, 100, 50)
        
        if this.IsClicked(okBtn, mouse) then
            // Save
            let newColors = Array.copy config.PlayerColors
            newColors.[targetPid] <- newColor
            currentScreen <- Settings { config with PlayerColors = newColors }
            
        if this.IsClicked(cancelBtn, mouse) then
            // Discard
            currentScreen <- Settings config
            
        // Only update screen state if color changed (to avoid flickering or stack overflow if we were recursively calling, but we use state machine so it's fine)
        match currentScreen with
        | ColorPicker(_, c, _) when c <> newColor -> 
             currentScreen <- ColorPicker(targetPid, newColor, config)
        | _ -> ()

        this.Window.Title <- sprintf "Dots and Boxes | Color Picker P%d" (targetPid + 1)


    member this.UpdateInGame(gameState: GameState, gameTime: GameTime) =
        let mouse = Mouse.GetState()
        let keyboard = Keyboard.GetState()

        if keyboard.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape) then
            currentScreen <- Settings gameState.Config
            
        if keyboard.IsKeyDown(Keys.R) && prevKeyboardState.IsKeyUp(Keys.R) then
             currentScreen <- InGame (initGame gameState.Config)

        let backBtn = new Rectangle(20, 20, 50, 50)
        if this.IsClicked(backBtn, mouse) then
             currentScreen <- Settings gameState.Config

        // Game Logic
        if mouse.LeftButton = ButtonState.Pressed && prevMouseState.LeftButton = ButtonState.Released then
            let gridWidth = gameState.Config.Cols * gameState.Config.Spacing
            let gridHeight = gameState.Config.Rows * gameState.Config.Spacing
            let offX = (this.GraphicsDevice.Viewport.Width - gridWidth) / 2
            let offY = (this.GraphicsDevice.Viewport.Height - gridHeight) / 2
            let spacing = gameState.Config.Spacing
            
            let relX = float (mouse.X - offX)
            let relY = float (mouse.Y - offY)
            
            let threshold = 15.0
            let mutable bestLine : LinePosition option = None
            let mutable minDist = Double.MaxValue
            
            // Check Horizontal lines
            for r in 0 .. gameState.Config.Rows do
                for c in 0 .. gameState.Config.Cols - 1 do
                    if relX >= float (c * spacing) && relX <= float ((c+1) * spacing) then
                        let dist = Math.Abs(relY - float (r * spacing))
                        if dist < threshold && dist < minDist then
                            minDist <- dist
                            bestLine <- Some { Pos = { Row = r; Col = c }; Orient = Horizontal }
            
            // Check Vertical lines
            for r in 0 .. gameState.Config.Rows - 1 do
                for c in 0 .. gameState.Config.Cols do
                    if relY >= float (r * spacing) && relY <= float ((r+1) * spacing) then
                        let dist = Math.Abs(relX - float (c * spacing))
                        if dist < threshold && dist < minDist then
                            minDist <- dist
                            bestLine <- Some { Pos = { Row = r; Col = c }; Orient = Vertical }

            match bestLine with
            | Some line -> 
                let nextState = tryPlaceLine gameState line
                currentScreen <- InGame nextState
            | None -> ()

        // Update Title
        let scoresStr = 
            gameState.Scores 
            |> Map.toList 
            |> List.map (fun (p, s) -> sprintf "P%d:%d" (p+1) s)
            |> String.concat " | "
            
        let status = if gameState.IsGameOver then "Game Over!" else sprintf "Turn: P%d" (gameState.CurrentTurn + 1)
        this.Window.Title <- sprintf "%s || %s || [R]etry [ESC]" scoresStr status

    override this.Update(gameTime) =
        match currentScreen with
        | Settings config -> this.UpdateSettings(config, gameTime)
        | ColorPicker (pid, color, config) -> this.UpdateColorPicker(pid, color, config)
        | InGame gameState -> this.UpdateInGame(gameState, gameTime)
        | _ -> ()

        prevMouseState <- Mouse.GetState()
        prevKeyboardState <- Keyboard.GetState()
        base.Update(gameTime)

    // --- Draw Logic ---
    
    member this.DrawRectangle(rect: Rectangle, color: Color) =
        spriteBatch.Draw(whitePixel, rect, color)

    // Helper to draw a gradient slider
    member this.DrawSlider(rect: Rectangle, value: byte, baseColorFunc: byte -> Color) =
        // Draw background gradient
        // Since we don't have gradient primitives easily, we draw strips
        let steps = 20
        let stepW = float32 rect.Width / float32 steps
        for i in 0 .. steps - 1 do
            let ratio = float i / float steps
            let cVal = byte (ratio * 255.0)
            let color = baseColorFunc cVal
            let x = float32 rect.X + float32 i * stepW
            let r = new Rectangle(int x, rect.Y, int (stepW + 1.0f), rect.Height)
            this.DrawRectangle(r, color)
            
        // Draw Border
        let top = new Rectangle(rect.X, rect.Y, rect.Width, 2)
        let bot = new Rectangle(rect.X, rect.Y + rect.Height - 2, rect.Width, 2)
        let left = new Rectangle(rect.X, rect.Y, 2, rect.Height)
        let right = new Rectangle(rect.X + rect.Width - 2, rect.Y, 2, rect.Height)
        this.DrawRectangle(top, Color.Gray)
        this.DrawRectangle(bot, Color.Gray)
        this.DrawRectangle(left, Color.Gray)
        this.DrawRectangle(right, Color.Gray)

        // Draw Knob
        let ratio = float value / 255.0
        let knobX = rect.X + int (ratio * float rect.Width) - 5
        let knobRect = new Rectangle(knobX, rect.Y - 5, 10, rect.Height + 10)
        this.DrawRectangle(knobRect, Color.White)
        // Knob border
        let border = 1
        let kInner = new Rectangle(knobRect.X + border, knobRect.Y + border, knobRect.Width - border*2, knobRect.Height - border*2)
        this.DrawRectangle(kInner, Color.Black)


    member this.DrawColorPicker(pid: int, color: Color) =
        let viewport = this.GraphicsDevice.Viewport
        let cX = viewport.Width / 2
        let cY = viewport.Height / 2
        
        // Draw Title "P<n> COLOR"
        drawText spriteBatch whitePixel (sprintf "P%d COLOR" (pid+1)) (Vector2(float32 cX - 80.0f, float32 cY - 150.0f)) 30.0f 4 Color.Black
        
        // Draw Preview
        let previewRect = new Rectangle(cX - 40, cY - 110, 80, 50)
        this.DrawRectangle(previewRect, color)
        
        // Draw Sliders
        let sliderWidth = 300
        let sliderHeight = 30
        let sliderX = cX - sliderWidth / 2
        let startY = cY - 50
        let spacing = 60
        
        let rRect = new Rectangle(sliderX, startY, sliderWidth, sliderHeight)
        let gRect = new Rectangle(sliderX, startY + spacing, sliderWidth, sliderHeight)
        let bRect = new Rectangle(sliderX, startY + spacing * 2, sliderWidth, sliderHeight)
        
        this.DrawSlider(rRect, color.R, (fun v -> Color(int v, 0, 0)))
        this.DrawSlider(gRect, color.G, (fun v -> Color(0, int v, 0)))
        this.DrawSlider(bRect, color.B, (fun v -> Color(0, 0, int v)))
        
        // Draw Values
        let valX = float32 (sliderX + sliderWidth + 10)
        drawNumber spriteBatch whitePixel (int color.R) (Vector2(valX, float32 startY)) 20.0f 2 Color.Black
        drawNumber spriteBatch whitePixel (int color.G) (Vector2(valX, float32 startY + float32 spacing)) 20.0f 2 Color.Black
        drawNumber spriteBatch whitePixel (int color.B) (Vector2(valX, float32 startY + float32 spacing * 2.0f)) 20.0f 2 Color.Black

        // Draw Buttons (Reset Removed)
        let btnY = startY + spacing * 3 + 20
        let okBtn = new Rectangle(cX - 110, btnY, 100, 50)
        let cancelBtn = new Rectangle(cX + 10, btnY, 100, 50)
        
        this.DrawRectangle(okBtn, Color.LightGreen)
        this.DrawRectangle(cancelBtn, Color.LightCoral)
        
        drawText spriteBatch whitePixel "OK" (Vector2(float32 okBtn.X + 25.0f, float32 okBtn.Y + 10.0f)) 25.0f 3 Color.White
        drawText spriteBatch whitePixel "BACK" (Vector2(float32 cancelBtn.X + 10.0f, float32 cancelBtn.Y + 15.0f)) 15.0f 3 Color.White


    member this.DrawSettings(config: GameConfig) =
        let viewport = this.GraphicsDevice.Viewport
        let cX = viewport.Width / 2
        let startY = viewport.Height / 2 - 150
        let getRowY index = startY + index * 70

        let drawControlRow index label value =
            let y = getRowY index
            let minusBtn = new Rectangle(cX - 150, y, 60, 60)
            let plusBtn = new Rectangle(cX + 90, y, 60, 60)
            
            this.DrawRectangle(minusBtn, Color.LightGray)
            this.DrawRectangle(plusBtn, Color.LightGray)
            
            drawSymbol spriteBatch whitePixel Minus minusBtn 4 Color.Black
            drawSymbol spriteBatch whitePixel Plus plusBtn 4 Color.Black
            
            // Draw Value
            let numSize = 30.0f
            let numStr = string value
            let textWidth = float32 (numStr.Length) * numSize * 1.2f
            drawNumber spriteBatch whitePixel value (Vector2(float32 cX - textWidth/2.0f, float32 y + 10.0f)) numSize 4 Color.Black

            // Draw Label
            let iconX = cX - 220
            let iconY = y + 15
            match label with
            | "ROWS" -> 
                let r = new Rectangle(iconX, iconY, 10, 30)
                this.DrawRectangle(r, Color.Black)
            | "COLS" ->
                let r = new Rectangle(iconX - 10, iconY + 10, 30, 10)
                this.DrawRectangle(r, Color.Black)
            | "PLAYERS" ->
                let r = new Rectangle(iconX, iconY, 20, 20)
                this.DrawRectangle(r, Color.Blue)
            | _ -> ()

        drawControlRow 0 "ROWS" config.Rows
        drawControlRow 1 "COLS" config.Cols
        drawControlRow 2 "PLAYERS" config.PlayerCount

        // Draw Color Pickers
        let colorStartY = getRowY 3 + 20
        let btnSize = 50
        let spacing = 10
        
        for i in 0 .. config.PlayerCount - 1 do
            let row = i / 4
            let col = i % 4
            let itemsInRow = 
                if row < (config.PlayerCount / 4) then 4 
                else config.PlayerCount % 4
            let itemsInRow = if itemsInRow = 0 then 4 else itemsInRow
            
            let rowWidth = itemsInRow * (btnSize + spacing) - spacing
            let startX = cX - rowWidth / 2
            
            let x = startX + col * (btnSize + spacing)
            let y = colorStartY + row * (btnSize + spacing)
            let rect = new Rectangle(x, y, btnSize, btnSize)
            
            // Draw color button
            this.DrawRectangle(rect, config.PlayerColors.[i])
            
            // Draw P<n> label above clearly
            drawText spriteBatch whitePixel (sprintf "P%d" (i+1)) (Vector2(float32 x + 5.0f, float32 y - 25.0f)) 15.0f 2 Color.Black

        // Play Button
        let playBtnY = colorStartY + (if config.PlayerCount > 4 then 2 else 1) * (btnSize + spacing) + 20
        let playBtn = new Rectangle(cX - 60, playBtnY, 120, 70)
        this.DrawRectangle(playBtn, Color.LightGreen)
        drawSymbol spriteBatch whitePixel Play playBtn 5 Color.White

    member this.DrawInGame(gameState: GameState) =
        let gridWidth = gameState.Config.Cols * gameState.Config.Spacing
        let gridHeight = gameState.Config.Rows * gameState.Config.Spacing
        let offX = (this.GraphicsDevice.Viewport.Width - gridWidth) / 2
        let offY = (this.GraphicsDevice.Viewport.Height - gridHeight) / 2
        let spacing = gameState.Config.Spacing
        
        // 1. Boxes
        for box in gameState.Boxes do
            let pos = box.Key
            let owner = box.Value
            let x = offX + pos.Col * spacing
            let y = offY + pos.Row * spacing
            let color = gameState.Config.PlayerColors.[owner]
            let rect = new Rectangle(x + 5, y + 5, spacing - 10, spacing - 10)
            this.DrawRectangle(rect, color * 0.5f)

        // 2. Lines
        for line in gameState.Lines do
            let pos = line.Key
            let color = gameState.Config.PlayerColors.[line.Value]
            let x = float32 (offX + pos.Pos.Col * spacing)
            let y = float32 (offY + pos.Pos.Row * spacing)
            let thick = gameState.Config.LineThickness
            
            if pos.Orient = Horizontal then
                let rect = new Rectangle(int x, int (y - float32 thick / 2.0f), spacing, thick)
                this.DrawRectangle(rect, color)
            else
                let rect = new Rectangle(int (x - float32 thick / 2.0f), int y, thick, spacing)
                this.DrawRectangle(rect, color)

        // 3. Dots
        for r in 0 .. gameState.Config.Rows do
            for c in 0 .. gameState.Config.Cols do
                let x = offX + c * spacing
                let y = offY + r * spacing
                let rRad = gameState.Config.DotRadius
                let rect = new Rectangle(x - rRad, y - rRad, rRad * 2, rRad * 2)
                this.DrawRectangle(rect, Color.Black)
                
        // 4. UI (Back Button)
        let backBtn = new Rectangle(20, 20, 50, 50)
        this.DrawRectangle(backBtn, Color.Gray)
        drawSymbol spriteBatch whitePixel Back backBtn 4 Color.White
        
        // 5. Current Turn Indicator
        let turnColor = gameState.Config.PlayerColors.[gameState.CurrentTurn]
        let turnRect = new Rectangle(this.GraphicsDevice.Viewport.Width - 70, 20, 50, 50)
        this.DrawRectangle(turnRect, turnColor)
        drawNumber spriteBatch whitePixel (gameState.CurrentTurn + 1) (Vector2(float32 turnRect.X + 15.0f, float32 turnRect.Y + 10.0f)) 20.0f 3 Color.White


        // 6. Game Over Overlay
        if gameState.IsGameOver then
            // Semi-transparent overlay
            let viewport = this.GraphicsDevice.Viewport
            let overlay = new Rectangle(0, 0, viewport.Width, viewport.Height)
            this.DrawRectangle(overlay, Color.White * 0.8f)
            
            // Draw Winner Message
            let msg, color = 
                match gameState.Winner with
                | Some pid -> (sprintf "P%d WIN" (pid + 1), gameState.Config.PlayerColors.[pid])
                | None -> ("DRAW", Color.Black)
            
            let charSize = 60.0f
            let textWidth = float32 msg.Length * charSize * 1.2f
            let pos = Vector2((float32 viewport.Width - textWidth) / 2.0f, (float32 viewport.Height - charSize * 1.5f) / 2.0f)
            
            drawText spriteBatch whitePixel msg pos charSize 8 color
            
            // Helper text to restart
            let subMsg = "PRESS R TO RESTART"
            let subSize = 20.0f
            let subWidth = float32 subMsg.Length * subSize * 1.2f
            let subPos = Vector2((float32 viewport.Width - subWidth) / 2.0f, pos.Y + 120.0f)
            drawText spriteBatch whitePixel subMsg subPos subSize 3 Color.Gray

    override this.Draw(gameTime) =
        this.GraphicsDevice.Clear(Color.WhiteSmoke)
        spriteBatch.Begin()
        
        match currentScreen with
        | Settings config -> this.DrawSettings(config)
        | ColorPicker (pid, color, config) -> this.DrawColorPicker(pid, color)
        | InGame gameState -> this.DrawInGame(gameState)
        | _ -> ()
        
        spriteBatch.End()
        base.Draw(gameTime)

[<EntryPoint>]
let main _ =
    use game = new DotsAndBoxesGame()
    game.Run()
    0