#!/usr/bin/env python3
import os
import sys

# Set environment variable for AVX2 detection
os.environ['PYGAME_DETECT_AVX2'] = '1'

# Import pygame first to avoid import issues
import pygame
from pygame.locals import *

# 色の定義
BLACK = (0, 0, 0)
WHITE = (255, 255, 255)
RED = (255, 0, 0)
GREEN = (0, 255, 0)
BLUE = (0, 0, 255)
LIGHT_BLUE = (173, 216, 230)
CREAM = (255, 253, 208)
YELLOW = (255, 255, 0)
ORANGE = (255, 165, 0)

# プレイヤーの色
PLAYER_COLORS = [
    (255, 50, 50),    # 赤
    (50, 50, 255),    # 青
    (50, 180, 50),    # 緑
    (255, 165, 0),    # オレンジ
    (180, 50, 180),   # 紫
    (0, 180, 180),    # ティール
    (180, 180, 0),    # オリーブ
    (255, 50, 255),   # マゼンタ
]

class SimpleColorPicker:
    def __init__(self, width=800, height=600, player_count=4):
        pygame.init()
        
        # 画面設定
        self.width = width
        self.height = height
        self.screen = pygame.display.set_mode((self.width, self.height))
        pygame.display.set_caption("Color Picker")
        
        # フォント設定
        self.font = pygame.font.SysFont(None, 36)
        self.small_font = pygame.font.SysFont(None, 24)
        
        # 色の設定
        self.player_colors = PLAYER_COLORS.copy()
        self.current_color_edit = 0
        self.player_count = player_count
        
        # ゲームクロック
        self.clock = pygame.time.Clock()
    
    def run(self):
        """カラーピッカーを実行し、選択された色のリストを返す"""
        running = True
        
        while running:
            for event in pygame.event.get():
                if event.type == QUIT:
                    return self.player_colors[:self.player_count]
                
                if event.type == MOUSEBUTTONDOWN and event.button == 1:
                    x, y = event.pos
                    
                    # カラーピッカーのウィンドウ位置
                    picker_width = 500
                    picker_height = 400
                    picker_x = self.width // 2 - picker_width // 2
                    picker_y = self.height // 2 - picker_height // 2
                    
                    # カラーパレットの位置
                    palette_width = 360
                    palette_height = 120
                    palette_x = picker_x + picker_width // 2 - palette_width // 2
                    palette_y = picker_y + 180
                    cell_size = 15
                    
                    # パレットクリック
                    if palette_x <= x < palette_x + palette_width and palette_y <= y < palette_y + palette_height:
                        col = (x - palette_x) // cell_size
                        row = (y - palette_y) // cell_size
                        
                        # HSVからRGBに変換
                        h = col / (palette_width // cell_size) * 360
                        s = 1.0
                        v = 1.0 - (row / (palette_height // cell_size) * 0.7)
                        
                        # HSVからRGBに変換（簡易版）
                        c = v * s
                        x_val = c * (1 - abs((h / 60) % 2 - 1))
                        m = v - c
                        
                        r, g, b = 0, 0, 0  # 初期値を設定
                        
                        if 0 <= h < 60:
                            r, g, b = c, x_val, 0
                        elif 60 <= h < 120:
                            r, g, b = x_val, c, 0
                        elif 120 <= h < 180:
                            r, g, b = 0, c, x_val
                        elif 180 <= h < 240:
                            r, g, b = 0, x_val, c
                        elif 240 <= h < 300:
                            r, g, b = x_val, 0, c
                        else:
                            r, g, b = c, 0, x_val
                        
                        r = int((r + m) * 255)
                        g = int((g + m) * 255)
                        b = int((b + m) * 255)
                        
                        # 現在のプレイヤーの色を更新
                        if self.current_color_edit < len(self.player_colors):
                            self.player_colors[self.current_color_edit] = (r, g, b)
                        else:
                            # 色が足りない場合は追加
                            while len(self.player_colors) <= self.current_color_edit:
                                self.player_colors.append(PLAYER_COLORS[len(self.player_colors) % len(PLAYER_COLORS)])
                            self.player_colors[self.current_color_edit] = (r, g, b)
                    
                    # 前へボタン
                    prev_button = pygame.Rect(picker_x + 100, picker_y + 320, 100, 40)
                    if prev_button.collidepoint(x, y):
                        self.current_color_edit = (self.current_color_edit - 1) % self.player_count
                    
                    # 次へボタン
                    next_button = pygame.Rect(picker_x + 300, picker_y + 320, 100, 40)
                    if next_button.collidepoint(x, y):
                        self.current_color_edit = (self.current_color_edit + 1) % self.player_count
                    
                    # 閉じるボタン
                    close_button = pygame.Rect(picker_x + picker_width // 2 - 50, picker_y + 320, 100, 40)
                    if close_button.collidepoint(x, y):
                        return self.player_colors[:self.player_count]
            
            # 背景を白に
            self.screen.fill(CREAM)
            
            # カラーピッカーのウィンドウ
            picker_width = 500
            picker_height = 400
            picker_x = self.width // 2 - picker_width // 2
            picker_y = self.height // 2 - picker_height // 2
            
            pygame.draw.rect(self.screen, (240, 240, 240), (picker_x, picker_y, picker_width, picker_height), border_radius=15)
            pygame.draw.rect(self.screen, BLACK, (picker_x, picker_y, picker_width, picker_height), 2, border_radius=15)
            
            # タイトル
            title = self.font.render("Color Picker", True, BLACK)
            self.screen.blit(title, (picker_x + picker_width // 2 - title.get_width() // 2, picker_y + 20))
            
            # プレイヤー選択
            player_text = self.font.render(f"Select color for Player {self.current_color_edit + 1}", True, BLACK)
            self.screen.blit(player_text, (picker_x + picker_width // 2 - player_text.get_width() // 2, picker_y + 70))
            
            # 現在の色を表示
            if self.current_color_edit < len(self.player_colors):
                current_color = self.player_colors[self.current_color_edit]
            else:
                # 色が足りない場合はデフォルト色を使用
                current_color = PLAYER_COLORS[self.current_color_edit % len(PLAYER_COLORS)]
                # 必要に応じて色リストを拡張
                while len(self.player_colors) <= self.current_color_edit:
                    self.player_colors.append(PLAYER_COLORS[len(self.player_colors) % len(PLAYER_COLORS)])
            
            pygame.draw.rect(self.screen, current_color, (picker_x + picker_width // 2 - 50, picker_y + 120, 100, 50), border_radius=10)
            pygame.draw.rect(self.screen, BLACK, (picker_x + picker_width // 2 - 50, picker_y + 120, 100, 50), 1, border_radius=10)
            
            # カラーパレットを描画
            palette_width = 360
            palette_height = 120
            palette_x = picker_x + picker_width // 2 - palette_width // 2
            palette_y = picker_y + 180
            
            # HSVカラーマップを描画
            cell_size = 15
            cols = palette_width // cell_size
            rows = palette_height // cell_size
            
            for row in range(rows):
                for col in range(cols):
                    # HSVからRGBに変換
                    h = col / cols * 360
                    s = 1.0
                    v = 1.0 - (row / rows * 0.7)  # 明るさを調整
                    
                    # HSVからRGBに変換（簡易版）
                    c = v * s
                    x = c * (1 - abs((h / 60) % 2 - 1))
                    m = v - c
                    
                    r, g, b = 0, 0, 0  # 初期値を設定
                    
                    if 0 <= h < 60:
                        r, g, b = c, x, 0
                    elif 60 <= h < 120:
                        r, g, b = x, c, 0
                    elif 120 <= h < 180:
                        r, g, b = 0, c, x
                    elif 180 <= h < 240:
                        r, g, b = 0, x, c
                    elif 240 <= h < 300:
                        r, g, b = x, 0, c
                    else:
                        r, g, b = c, 0, x
                    
                    r = int((r + m) * 255)
                    g = int((g + m) * 255)
                    b = int((b + m) * 255)
                    
                    cell_rect = pygame.Rect(
                        palette_x + col * cell_size,
                        palette_y + row * cell_size,
                        cell_size,
                        cell_size
                    )
                    pygame.draw.rect(self.screen, (r, g, b), cell_rect)
            
            # パレット枠を描画
            pygame.draw.rect(self.screen, BLACK, (palette_x, palette_y, palette_width, palette_height), 1)
            
            # プレイヤー切り替えボタン
            prev_button = pygame.Rect(picker_x + 100, picker_y + 320, 100, 40)
            pygame.draw.rect(self.screen, LIGHT_BLUE, prev_button, border_radius=10)
            pygame.draw.rect(self.screen, BLACK, prev_button, 2, border_radius=10)
            prev_text = self.small_font.render("Previous", True, BLACK)
            self.screen.blit(prev_text, (prev_button.centerx - prev_text.get_width() // 2, prev_button.centery - prev_text.get_height() // 2))
            
            next_button = pygame.Rect(picker_x + 300, picker_y + 320, 100, 40)
            pygame.draw.rect(self.screen, LIGHT_BLUE, next_button, border_radius=10)
            pygame.draw.rect(self.screen, BLACK, next_button, 2, border_radius=10)
            next_text = self.small_font.render("Next", True, BLACK)
            self.screen.blit(next_text, (next_button.centerx - next_text.get_width() // 2, next_button.centery - next_text.get_height() // 2))
            
            # 閉じるボタン
            close_button = pygame.Rect(picker_x + picker_width // 2 - 50, picker_y + 320, 100, 40)
            pygame.draw.rect(self.screen, (100, 200, 100), close_button, border_radius=10)
            pygame.draw.rect(self.screen, BLACK, close_button, 2, border_radius=10)
            close_text = self.small_font.render("Close", True, BLACK)
            self.screen.blit(close_text, (close_button.centerx - close_text.get_width() // 2, close_button.centery - close_text.get_height() // 2))
            
            pygame.display.flip()
            self.clock.tick(60)
        
        return self.player_colors[:self.player_count]

# Import the game
from mine_dots_and_boxes import MinedDotsAndBoxes

# Override the handle_settings_events method to fix the color picker issue
def fixed_handle_settings_events(self, event):
    """設定画面のイベント処理（修正版）"""
    if event.type == MOUSEBUTTONDOWN and event.button == 1:  # 左クリックのみ処理
        x, y = event.pos
        
        # 設定画面の中央位置
        settings_width = 500
        settings_height = 400
        settings_x = self.width // 2 - settings_width // 2
        settings_y = self.height // 2 - settings_height // 2
        
        # 行数の増減ボタン
        if settings_x + 200 <= x <= settings_x + 230 and settings_y + 100 <= y <= settings_y + 130:
            self.settings_rows = max(2, self.settings_rows - 1)
            # 行数が減ったら爆弾数も調整
            max_mines = self.settings_rows * self.settings_cols // 2
            self.settings_mines = min(self.settings_mines, max_mines)
        elif settings_x + 270 <= x <= settings_x + 300 and settings_y + 100 <= y <= settings_y + 130:
            self.settings_rows = min(10, self.settings_rows + 1)
        
        # 列数の増減ボタン
        elif settings_x + 200 <= x <= settings_x + 230 and settings_y + 150 <= y <= settings_y + 180:
            self.settings_cols = max(2, self.settings_cols - 1)
            # 列数が減ったら爆弾数も調整
            max_mines = self.settings_rows * self.settings_cols // 2
            self.settings_mines = min(self.settings_mines, max_mines)
        elif settings_x + 270 <= x <= settings_x + 300 and settings_y + 150 <= y <= settings_y + 180:
            self.settings_cols = min(10, self.settings_cols + 1)
        
        # 爆弾数の増減ボタン
        elif settings_x + 200 <= x <= settings_x + 230 and settings_y + 200 <= y <= settings_y + 230:
            self.settings_mines = max(0, self.settings_mines - 1)
        elif settings_x + 270 <= x <= settings_x + 300 and settings_y + 200 <= y <= settings_y + 230:
            max_mines = self.settings_rows * self.settings_cols // 2
            self.settings_mines = min(max_mines, self.settings_mines + 1)
        
        # プレイヤー数の増減ボタン
        elif settings_x + 200 <= x <= settings_x + 230 and settings_y + 250 <= y <= settings_y + 280:
            self.settings_players = max(2, self.settings_players - 1)
        elif settings_x + 270 <= x <= settings_x + 300 and settings_y + 250 <= y <= settings_y + 280:
            self.settings_players = min(8, self.settings_players + 1)
        
        # カラー設定ボタン
        elif settings_x + 150 <= x <= settings_x + 350 and settings_y + 300 <= y <= settings_y + 330:
            # 別のカラーピッカーを使用
            picker = SimpleColorPicker(self.width, self.height, self.settings_players)
            self.player_line_colors = picker.run()
        
        # スタートボタン
        elif settings_x + 150 <= x <= settings_x + 350 and settings_y + 350 <= y <= settings_y + 380:
            self.start_game_with_settings()
    
    elif event.type == KEYDOWN:
        if event.key == K_ESCAPE:
            self.in_settings = False

# Run the game
if __name__ == "__main__":
    game = MinedDotsAndBoxes()
    
    # Replace the handle_settings_events method with our fixed version
    game.handle_settings_events = fixed_handle_settings_events.__get__(game, MinedDotsAndBoxes)
    
    # Run the game
    game.run()
