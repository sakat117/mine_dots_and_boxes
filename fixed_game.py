#!/usr/bin/env python3
import os

# Set environment variable for AVX2 detection
os.environ['PYGAME_DETECT_AVX2'] = '1'

# Import pygame first to avoid import issues
import pygame

# Import the game
from mine_dots_and_boxes import MinedDotsAndBoxes

# Override the handle_settings_events method to fix the color picker issue
def fixed_handle_settings_events(self, event):
    """設定画面のイベント処理（修正版）"""
    if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:  # 左クリックのみ処理
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
            print("カラー設定ボタンがクリックされました")
            self.in_settings = False  # 設定画面を閉じる
            self.show_color_picker = True  # カラーピッカーを表示
            self.current_color_edit = 0
            print(f"状態変更: in_settings={self.in_settings}, show_color_picker={self.show_color_picker}")
        
        # スタートボタン
        elif settings_x + 150 <= x <= settings_x + 350 and settings_y + 350 <= y <= settings_y + 380:
            self.start_game_with_settings()
    
    elif event.type == pygame.KEYDOWN:
        if event.key == pygame.K_ESCAPE:
            self.in_settings = False

# Run the game
if __name__ == "__main__":
    game = MinedDotsAndBoxes()
    
    # Replace the handle_settings_events method with our fixed version
    game.handle_settings_events = fixed_handle_settings_events.__get__(game, MinedDotsAndBoxes)
    
    # Run the game
    game.run()
