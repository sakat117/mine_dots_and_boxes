module MineDotsAndBoxesFs.VectorGraphics

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open System

// Helper to draw a line strip
let private drawStrip (batch: SpriteBatch) (pixel: Texture2D) (points: Vector2 list) (thick: int) (color: Color) =
    for i in 0 .. points.Length - 2 do
        let p1 = points.[i]
        let p2 = points.[i+1]
        let edge = p2 - p1
        let angle = float32 (Math.Atan2(float edge.Y, float edge.X))
        let length = edge.Length()
        
        batch.Draw(
            pixel,
            new Rectangle(int p1.X, int p1.Y, int length, thick),
            Nullable<Rectangle>(),
            color,
            angle,
            Vector2.Zero,
            SpriteEffects.None,
            0.0f
        )

// Draw a single digit (0-9) or letter (P, W, I, N, D, R, A)
// Mapping: 0-9 are digits. 10=P, 11=W, 12=I, 13=N, 14=D, 15=R, 16=A
let drawChar (batch: SpriteBatch) (pixel: Texture2D) (code: int) (pos: Vector2) (size: float32) (thick: int) (color: Color) =
    let w = size
    let h = size * 1.5f
    
    // Define points relative to 0,0
    let tl = Vector2(0.0f, 0.0f)
    let tr = Vector2(w, 0.0f)
    let tm = Vector2(w/2.0f, 0.0f)
    let mr = Vector2(w, h / 2.0f)
    let ml = Vector2(0.0f, h / 2.0f)
    let mm = Vector2(w/2.0f, h / 2.0f)
    let bl = Vector2(0.0f, h)
    let br = Vector2(w, h)
    let bm = Vector2(w/2.0f, h)
    
    let segments = 
        match code with
        | 0 -> [[tl; tr; br; bl; tl]]
        | 1 -> [[tm; bm]] 
        | 2 -> [[tl; tr; mr; ml; bl; br]]
        | 3 -> [[tl; tr; mr; br; bl]; [ml; mr]]
        | 4 -> [[tl; ml; mr]; [tr; br]]
        | 5 -> [[tr; tl; ml; mr; br; bl]]
        | 6 -> [[tr; tl; bl; br; mr; ml]]
        | 7 -> [[tl; tr; br]]
        | 8 -> [[tl; tr; br; bl; tl]; [ml; mr]]
        | 9 -> [[bl; br; tr; tl; ml; mr]]
        | 10 -> [[bl; tl; tr; mr; ml]] // P
        | 11 -> [[tl; bl; mm; br; tr]] // W
        | 12 -> [[tl; tr]; [bl; br]; [tm; bm]] // I
        | 13 -> [[bl; tl; br; tr]] // N
        | 14 -> [[tl; tr; br; bl; tl]] // D (Same as 0 for now, or curve it? square is fine)
        | 15 -> [[bl; tl; tr; mr; ml]; [Vector2(w*0.2f, h/2.0f); br]] // R (Loop + Leg)
        | 16 -> [[bl; tl; tr; br]; [ml; mr]] // A
        | _ -> [] 

    for strip in segments do
        let absStrip = strip |> List.map (fun p -> p + pos)
        drawStrip batch pixel absStrip thick color

// Draw a number
let drawNumber (batch: SpriteBatch) (pixel: Texture2D) (number: int) (pos: Vector2) (size: float32) (thick: int) (color: Color) =
    let str = string number
    let spacing = size * 1.2f
    
    str |> Seq.iteri (fun i c ->
        let digit = int (Char.GetNumericValue c)
        let p = Vector2(pos.X + float32 i * spacing, pos.Y)
        drawChar batch pixel digit p size thick color
    )

// Draw custom text based on supported chars
let drawText (batch: SpriteBatch) (pixel: Texture2D) (text: string) (pos: Vector2) (size: float32) (thick: int) (color: Color) =
    let spacing = size * 1.2f
    
    text |> Seq.iteri (fun i c ->
        let code = 
            match Char.ToUpper c with
            | 'P' -> 10
            | 'W' -> 11
            | 'I' -> 12
            | 'N' -> 13
            | 'D' -> 14
            | 'R' -> 15
            | 'A' -> 16
            | d when Char.IsDigit d -> int (Char.GetNumericValue d)
            | _ -> -1
            
        if code >= 0 then
            let p = Vector2(pos.X + float32 i * spacing, pos.Y)
            drawChar batch pixel code p size thick color
    )


// Draw a generic symbol
type Symbol = Plus | Minus | Play | Back

let drawSymbol (batch: SpriteBatch) (pixel: Texture2D) (symbol: Symbol) (rect: Rectangle) (thick: int) (color: Color) =
    let cX = float32 rect.X + float32 rect.Width / 2.0f
    let cY = float32 rect.Y + float32 rect.Height / 2.0f
    let s = float32 (Math.Min(rect.Width, rect.Height)) * 0.6f // Scale
    
    match symbol with
    | Plus ->
        drawStrip batch pixel [Vector2(cX - s/2.0f, cY); Vector2(cX + s/2.0f, cY)] thick color
        drawStrip batch pixel [Vector2(cX, cY - s/2.0f); Vector2(cX, cY + s/2.0f)] thick color
    | Minus ->
        drawStrip batch pixel [Vector2(cX - s/2.0f, cY); Vector2(cX + s/2.0f, cY)] thick color
    | Play ->
        // Triangle pointing right
        let p1 = Vector2(cX - s/2.0f, cY - s/2.0f)
        let p2 = Vector2(cX + s/2.0f, cY)
        let p3 = Vector2(cX - s/2.0f, cY + s/2.0f)
        drawStrip batch pixel [p1; p2; p3; p1] thick color
    | Back ->
         // Arrow pointing left
        let p1 = Vector2(cX + s/2.0f, cY)
        let p2 = Vector2(cX - s/2.0f, cY)
        let p3 = Vector2(cX, cY - s/2.0f) // Arrow head top
        let p4 = Vector2(cX, cY + s/2.0f) // Arrow head bottom
        drawStrip batch pixel [p1; p2] thick color
        drawStrip batch pixel [p3; p2; p4] thick color