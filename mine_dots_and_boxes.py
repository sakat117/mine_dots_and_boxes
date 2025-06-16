import sys
import random
import math
import os

# Set environment variable for AVX2 detection
os.environ['PYGAME_DETECT_AVX2'] = '1'

import pygame
from pygame.locals import *

# 色の定義
BLACK = (0, 0, 0)
WHITE = (255, 255, 255)
RED = (255, 0, 0)
GREEN = (0, 255, 0)
BLUE = (0, 0, 255)
LIGHT_GRAY = (200, 200, 200)
DARK_GRAY = (100, 100, 100)
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

class MinedDotsAndBoxes:
    def __init__(self, settings=None):
        pygame.init()
        
        # 設定を引き継ぐ
        if settings:
            # 前回の設定を引き継ぐ
            self.box_size = settings.get('box_size', 80)
            self.dot_size = settings.get('dot_size', 5)
            self.margin = settings.get('margin', 50)
            self.rows = settings.get('rows', 5)
            self.cols = settings.get('cols', 5)
            self.mine_count = settings.get('mine_count', 3)
            self.player_count = settings.get('player_count', 2)
            self.player_line_colors = settings.get('player_line_colors', PLAYER_COLORS[:self.player_count].copy())
        else:
            # 初期設定
            self.box_size = 80
            self.dot_size = 5
            self.margin = 50
            self.rows = 5
            self.cols = 5
            self.mine_count = 3
            self.player_count = 2
            self.player_line_colors = PLAYER_COLORS[:self.player_count].copy()
        
        # 画面設定
        self.width = max(800, self.cols * self.box_size + self.margin * 2)
        self.height = max(600, self.rows * self.box_size + self.margin * 2 + 150)
        self.screen = pygame.display.set_mode((self.width, self.height), pygame.RESIZABLE)
        pygame.display.set_caption("Mined Dots and Boxes")
        
        # フォント設定
        try:
            # 日本語対応のためにSysFont使用
            self.font = pygame.font.SysFont(None, 36)
            self.small_font = pygame.font.SysFont(None, 24)
        except:
            self.font = pygame.font.SysFont(None, 36)
            self.small_font = pygame.font.SysFont(None, 24)
        
        # ゲーム状態
        self.current_player = 0
        self.players_alive = [True] * self.player_count
        self.player_scores = [0] * self.player_count
        self.game_over = False
        self.winner = -1
        self.completed_box_this_turn = False
        
        # 爆発したプレイヤーと爆発したボックスの記録
        self.exploded_players = []  # 爆発したプレイヤーのインデックス
        self.exploded_boxes = []    # 爆発したボックスの座標 (row, col)
        
        # 線の状態
        self.h_lines = [[False for _ in range(self.cols)] for _ in range(self.rows + 1)]
        self.v_lines = [[False for _ in range(self.cols + 1)] for _ in range(self.rows)]
        
        # 線の所有者を記録
        self.h_line_owners = [[-1 for _ in range(self.cols)] for _ in range(self.rows + 1)]
        self.v_line_owners = [[-1 for _ in range(self.cols + 1)] for _ in range(self.rows)]
        
        # 箱の所有者と爆弾の配置
        self.boxes = [[-1 for _ in range(self.cols)] for _ in range(self.rows)]
        self.mines = [[False for _ in range(self.cols)] for _ in range(self.rows)]
        
        # 点滅効果用
        self.flash_counter = 0
        self.flashing_lines = set()  # 点滅する線の集合 {(line_type, row, col), ...}
        self.mine_adjacent_lines = set()  # 爆弾に隣接する線の集合 {(line_type, row, col), ...}
        
        # 爆弾を配置
        self.place_mines()
        
        # 設定画面
        self.in_settings = True if not settings else False  # 初回起動時のみ設定画面を表示
        self.settings_rows = self.rows
        self.settings_cols = self.cols
        self.settings_mines = self.mine_count
        self.settings_players = self.player_count
        
        # カラーピッカー
        self.show_color_picker = False
        self.current_color_edit = 0
        
        # 背景色
        self.bg_color = CREAM
        
        # ボックススタイル
        self.box_style = {
            'border_radius': 12,  # 角の丸み
            'gradient': True      # グラデーション効果
        }
        
        # デバッグ設定
        self.show_mines = False  # 爆弾を表示するかどうか
        
        # ゲームクロック
        self.clock = pygame.time.Clock()
    
    def place_mines(self):
        """爆弾をランダムに配置する"""
        # 爆弾の位置をリセット
        self.mines = [[False for _ in range(self.cols)] for _ in range(self.rows)]
        
        mine_positions = []
        while len(mine_positions) < self.mine_count:
            row = random.randint(0, self.rows - 1)
            col = random.randint(0, self.cols - 1)
            if not self.mines[row][col]:
                self.mines[row][col] = True
                mine_positions.append((row, col))
        
        # 爆弾に隣接する線を更新
        self.update_mine_adjacent_lines()
    
    def update_mine_adjacent_lines(self):
        """爆弾に隣接するすべての線を更新する"""
        # 爆弾に隣接する線の集合をクリア
        self.mine_adjacent_lines.clear()
        
        # ゲームボード上のすべての爆弾の位置を確認し、隣接する線を記録
        for row in range(self.rows):
            for col in range(self.cols):
                if self.mines[row][col]:
                    # この爆弾に隣接する4つの線を記録
                    # 上の線
                    self.mine_adjacent_lines.add(("h", row, col))
                    
                    # 下の線
                    self.mine_adjacent_lines.add(("h", row+1, col))
                    
                    # 左の線
                    self.mine_adjacent_lines.add(("v", row, col))
                    
                    # 右の線
                    self.mine_adjacent_lines.add(("v", row, col+1))
    
    def is_mine_adjacent(self, line_type, row, col):
        """指定された線が爆弾に隣接しているかチェックする"""
        return (line_type, row, col) in self.mine_adjacent_lines
    
    def update_flashing_lines(self):
        """点滅効果を更新する"""
        self.flash_counter = (self.flash_counter + 1) % 60  # 点滅周期を長くする
    
    def is_line_flashing(self, line_type, row, col):
        """指定された線が点滅中かどうかをチェックする"""
        # 点滅する線の集合に含まれているかチェック
        return (line_type, row, col) in self.flashing_lines and self.flash_counter < 30
    
    def check_box_completion(self, row, col):
        """指定されたボックスが完成したかチェックする"""
        if row < 0 or row >= self.rows or col < 0 or col >= self.cols:
            return False
        
        # ボックスの4辺をチェック
        top = self.h_lines[row][col]
        bottom = self.h_lines[row+1][col]
        left = self.v_lines[row][col]
        right = self.v_lines[row][col+1]
        
        return top and bottom and left and right
    
    def handle_game_events(self, event):
        """ゲームイベントを処理する"""
        if event.type == MOUSEBUTTONDOWN and event.button == 1:
            if self.game_over:
                return
            
            x, y = event.pos
            
            # 水平線のクリック判定
            for row in range(self.rows + 1):
                for col in range(self.cols):
                    x1 = self.margin + col * self.box_size + self.dot_size
                    y1 = self.margin + row * self.box_size
                    x2 = self.margin + (col + 1) * self.box_size - self.dot_size
                    y2 = y1
                    
                    # 線の周りの当たり判定領域
                    line_rect = pygame.Rect(x1, y1 - 5, x2 - x1, 10)
                    
                    if line_rect.collidepoint(x, y) and not self.h_lines[row][col]:
                        self.h_lines[row][col] = True
                        self.h_line_owners[row][col] = self.current_player  # 線の所有者を記録
                        
                        # 爆弾に隣接する線をクリックした場合、点滅リストに追加
                        if self.is_mine_adjacent("h", row, col):
                            self.flashing_lines.add(("h", row, col))
                        
                        # ボックス完成チェック
                        completed_box = False
                        
                        # 上のボックスをチェック
                        if row > 0 and self.check_box_completion(row-1, col):
                            self.boxes[row-1][col] = self.current_player
                            self.player_scores[self.current_player] += 1
                            completed_box = True
                            
                            # 爆弾があるボックスを完成させた場合は爆発
                            if self.mines[row-1][col]:
                                self.players_alive[self.current_player] = False
                                self.exploded_players.append(self.current_player)
                                self.exploded_boxes.append((row-1, col))
                        
                        # 下のボックスをチェック
                        if row < self.rows and self.check_box_completion(row, col):
                            self.boxes[row][col] = self.current_player
                            self.player_scores[self.current_player] += 1
                            completed_box = True
                            
                            # 爆弾があるボックスを完成させた場合は爆発
                            if self.mines[row][col]:
                                self.players_alive[self.current_player] = False
                                self.exploded_players.append(self.current_player)
                                self.exploded_boxes.append((row, col))
                        
                        # ボックスを完成させた場合は点滅エフェクトを追加
                        if completed_box:
                            self.flashing_lines.add(("h", row, col))
                            self.completed_box_this_turn = True
                        
                        # ボックスを完成させなかった場合は次のプレイヤーへ
                        if not completed_box:
                            self.next_player()
                        
                        # ゲーム終了チェック
                        self.check_game_over()
                        return
            
            # 垂直線のクリック判定
            for row in range(self.rows):
                for col in range(self.cols + 1):
                    x1 = self.margin + col * self.box_size
                    y1 = self.margin + row * self.box_size + self.dot_size
                    x2 = x1
                    y2 = self.margin + (row + 1) * self.box_size - self.dot_size
                    
                    # 線の周りの当たり判定領域
                    line_rect = pygame.Rect(x1 - 5, y1, 10, y2 - y1)
                    
                    if line_rect.collidepoint(x, y) and not self.v_lines[row][col]:
                        self.v_lines[row][col] = True
                        self.v_line_owners[row][col] = self.current_player  # 線の所有者を記録
                        
                        # 爆弾に隣接する線をクリックした場合、点滅リストに追加
                        if self.is_mine_adjacent("v", row, col):
                            self.flashing_lines.add(("v", row, col))
                        
                        # ボックス完成チェック
                        completed_box = False
                        
                        # 左のボックスをチェック
                        if col > 0 and self.check_box_completion(row, col-1):
                            self.boxes[row][col-1] = self.current_player
                            self.player_scores[self.current_player] += 1
                            completed_box = True
                            
                            # 爆弾があるボックスを完成させた場合は爆発
                            if self.mines[row][col-1]:
                                self.players_alive[self.current_player] = False
                                self.exploded_players.append(self.current_player)
                                self.exploded_boxes.append((row, col-1))
                        
                        # 右のボックスをチェック
                        if col < self.cols and self.check_box_completion(row, col):
                            self.boxes[row][col] = self.current_player
                            self.player_scores[self.current_player] += 1
                            completed_box = True
                            
                            # 爆弾があるボックスを完成させた場合は爆発
                            if self.mines[row][col]:
                                self.players_alive[self.current_player] = False
                                self.exploded_players.append(self.current_player)
                                self.exploded_boxes.append((row, col))
                        
                        # ボックスを完成させた場合は点滅エフェクトを追加
                        if completed_box:
                            self.flashing_lines.add(("v", row, col))
                            self.completed_box_this_turn = True
                        
                        # ボックスを完成させなかった場合は次のプレイヤーへ
                        if not completed_box:
                            self.next_player()
                        
                        # ゲーム終了チェック
                        self.check_game_over()
                        return
        
        elif event.type == KEYDOWN:
            if event.key == K_r and self.game_over:
                # ゲームをリセット（設定を引き継ぐ）
                settings = {
                    'rows': self.rows,
                    'cols': self.cols,
                    'mine_count': self.mine_count,
                    'player_count': self.player_count,
                    'player_line_colors': self.player_line_colors,
                    'box_size': self.box_size,
                    'dot_size': self.dot_size,
                    'margin': self.margin
                }
                self.__init__(settings)
            elif event.key == K_ESCAPE:
                # ESCキーで設定画面を表示
                self.in_settings = True
            elif event.key == K_d:
                # デバッグモード切り替え
                self.show_mines = not self.show_mines
    
    def next_player(self):
        """次のプレイヤーに切り替える"""
        self.completed_box_this_turn = False
        
        # 次のプレイヤーを見つける
        original_player = self.current_player
        while True:
            self.current_player = (self.current_player + 1) % self.player_count
            if self.players_alive[self.current_player]:
                break
            
            # 全員死んでいる場合は元のプレイヤーに戻る
            if self.current_player == original_player:
                self.game_over = True
                self.determine_winner()
                break
    
    def check_game_over(self):
        """ゲーム終了条件をチェックする"""
        # すべての非爆弾ボックスが埋まっているかチェック
        all_non_mine_boxes_filled = True
        for row in range(self.rows):
            for col in range(self.cols):
                # 爆弾でないボックスが埋まっていないかチェック
                if not self.mines[row][col] and self.boxes[row][col] == -1:
                    all_non_mine_boxes_filled = False
                    break
            if not all_non_mine_boxes_filled:
                break
        
        # 生きているプレイヤーが1人以下かチェック
        alive_players = sum(self.players_alive)
        
        if all_non_mine_boxes_filled or alive_players <= 1:
            self.game_over = True
            self.determine_winner()
            return True
        return False
    
    def determine_winner(self):
        """勝者を決定する"""
        max_score = -1
        winner = -1
        tie = False
        
        for i in range(self.player_count):
            if self.players_alive[i] and self.player_scores[i] > max_score:
                max_score = self.player_scores[i]
                winner = i
                tie = False
            elif self.players_alive[i] and self.player_scores[i] == max_score:
                tie = True
        
        if not tie:
            self.winner = winner
    
    def handle_settings_events(self, event):
        """設定画面のイベント処理"""
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
                # 設定画面を閉じてからカラーピッカーを表示
                self.in_settings = False
                self.show_color_picker = True
                self.current_color_edit = 0
            
            # スタートボタン
            elif settings_x + 150 <= x <= settings_x + 350 and settings_y + 350 <= y <= settings_y + 380:
                self.start_game_with_settings()
        
        elif event.type == KEYDOWN:
            if event.key == K_ESCAPE:
                self.in_settings = False
    
    def draw_settings_screen(self):
        """設定画面を描画する"""
        # 背景を暗くする
        overlay = pygame.Surface((self.width, self.height), pygame.SRCALPHA)
        overlay.fill((0, 0, 0, 180))  # 半透明の黒色
        self.screen.blit(overlay, (0, 0))
        
        # 設定ウィンドウ
        settings_width = 500
        settings_height = 400
        settings_x = self.width // 2 - settings_width // 2
        settings_y = self.height // 2 - settings_height // 2
        
        pygame.draw.rect(self.screen, (240, 240, 240), (settings_x, settings_y, settings_width, settings_height), border_radius=15)
        pygame.draw.rect(self.screen, BLACK, (settings_x, settings_y, settings_width, settings_height), 2, border_radius=15)
        
        # タイトル
        title = self.font.render("Game Settings", True, BLACK)
        self.screen.blit(title, (settings_x + settings_width // 2 - title.get_width() // 2, settings_y + 20))
        
        # 行数設定
        rows_text = self.font.render("Rows:", True, BLACK)
        self.screen.blit(rows_text, (settings_x + 50, settings_y + 100))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 200, settings_y + 100, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 200, settings_y + 100, 30, 30), 2, border_radius=5)
        minus = self.font.render("-", True, BLACK)
        self.screen.blit(minus, (settings_x + 210, settings_y + 100))
        
        rows_value = self.font.render(str(self.settings_rows), True, BLACK)
        self.screen.blit(rows_value, (settings_x + 240, settings_y + 100))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 270, settings_y + 100, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 270, settings_y + 100, 30, 30), 2, border_radius=5)
        plus = self.font.render("+", True, BLACK)
        self.screen.blit(plus, (settings_x + 280, settings_y + 100))
        
        # 列数設定
        cols_text = self.font.render("Columns:", True, BLACK)
        self.screen.blit(cols_text, (settings_x + 50, settings_y + 150))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 200, settings_y + 150, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 200, settings_y + 150, 30, 30), 2, border_radius=5)
        self.screen.blit(minus, (settings_x + 210, settings_y + 150))
        
        cols_value = self.font.render(str(self.settings_cols), True, BLACK)
        self.screen.blit(cols_value, (settings_x + 240, settings_y + 150))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 270, settings_y + 150, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 270, settings_y + 150, 30, 30), 2, border_radius=5)
        self.screen.blit(plus, (settings_x + 280, settings_y + 150))
        
        # 爆弾数設定
        mines_text = self.font.render("Mines:", True, BLACK)
        self.screen.blit(mines_text, (settings_x + 50, settings_y + 200))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 200, settings_y + 200, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 200, settings_y + 200, 30, 30), 2, border_radius=5)
        self.screen.blit(minus, (settings_x + 210, settings_y + 200))
        
        mines_value = self.font.render(str(self.settings_mines), True, BLACK)
        self.screen.blit(mines_value, (settings_x + 240, settings_y + 200))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 270, settings_y + 200, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 270, settings_y + 200, 30, 30), 2, border_radius=5)
        self.screen.blit(plus, (settings_x + 280, settings_y + 200))
        
        # プレイヤー数設定
        players_text = self.small_font.render("Players:", True, BLACK)
        self.screen.blit(players_text, (settings_x + 50, settings_y + 250))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 200, settings_y + 250, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 200, settings_y + 250, 30, 30), 2, border_radius=5)
        self.screen.blit(minus, (settings_x + 210, settings_y + 250))
        
        players_value = self.font.render(str(self.settings_players), True, BLACK)
        self.screen.blit(players_value, (settings_x + 240, settings_y + 250))
        
        pygame.draw.rect(self.screen, LIGHT_GRAY, (settings_x + 270, settings_y + 250, 30, 30), border_radius=5)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 270, settings_y + 250, 30, 30), 2, border_radius=5)
        self.screen.blit(plus, (settings_x + 280, settings_y + 250))
        
        # カラー設定ボタン
        pygame.draw.rect(self.screen, LIGHT_BLUE, (settings_x + 150, settings_y + 300, 200, 30), border_radius=10)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 150, settings_y + 300, 200, 30), 2, border_radius=10)
        color_text = self.small_font.render("Player Colors", True, BLACK)
        self.screen.blit(color_text, (settings_x + 250 - color_text.get_width() // 2, settings_y + 305))
        
        # スタートボタン
        pygame.draw.rect(self.screen, (100, 200, 100), (settings_x + 150, settings_y + 350, 200, 30), border_radius=10)
        pygame.draw.rect(self.screen, BLACK, (settings_x + 150, settings_y + 350, 200, 30), 2, border_radius=10)
        start_text = self.font.render("Start Game", True, BLACK)
        self.screen.blit(start_text, (settings_x + 250 - start_text.get_width() // 2, settings_y + 350))
        
        pygame.display.flip()
    
    def draw_color_picker(self):
        """カラーピッカーを描画する"""
        try:
            # カラーピッカーの背景
            overlay = pygame.Surface((self.width, self.height), pygame.SRCALPHA)
            overlay.fill((0, 0, 0, 180))  # 半透明の黒色
            self.screen.blit(overlay, (0, 0))
            
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
            if self.current_color_edit < len(self.player_line_colors):
                current_color = self.player_line_colors[self.current_color_edit]
            else:
                # 色が足りない場合はデフォルト色を使用
                current_color = PLAYER_COLORS[self.current_color_edit % len(PLAYER_COLORS)]
                # 必要に応じて色リストを拡張
                while len(self.player_line_colors) <= self.current_color_edit:
                    self.player_line_colors.append(PLAYER_COLORS[len(self.player_line_colors) % len(PLAYER_COLORS)])
            
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
            return {
                'palette': (palette_x, palette_y, palette_width, palette_height),
                'cell_size': cell_size,
                'prev_button': prev_button,
                'next_button': next_button,
                'close_button': close_button
            }
        except Exception as e:
            print(f"カラーピッカー描画エラー: {e}")
            import traceback
            traceback.print_exc()
            # エラーが発生した場合はカラーピッカーを閉じる
            self.show_color_picker = False
            return None
    def handle_color_picker_events(self, event):
        """カラーピッカーのイベント処理"""
        if event.type == MOUSEBUTTONDOWN and event.button == 1:  # 左クリックのみ処理
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
                if self.current_color_edit < len(self.player_line_colors):
                    self.player_line_colors[self.current_color_edit] = (r, g, b)
                else:
                    # 色が足りない場合は追加
                    while len(self.player_line_colors) <= self.current_color_edit:
                        self.player_line_colors.append(PLAYER_COLORS[len(self.player_line_colors) % len(PLAYER_COLORS)])
                    self.player_line_colors[self.current_color_edit] = (r, g, b)
            
            # 前へボタン
            prev_button = pygame.Rect(picker_x + 100, picker_y + 320, 100, 40)
            if prev_button.collidepoint(x, y):
                # 修正: 正しく前のプレイヤーに移動
                self.current_color_edit = (self.current_color_edit - 1)
                if self.current_color_edit < 0:
                    self.current_color_edit = self.settings_players - 1
            
            # 次へボタン
            next_button = pygame.Rect(picker_x + 300, picker_y + 320, 100, 40)
            if next_button.collidepoint(x, y):
                # 修正: 正しく次のプレイヤーに移動
                self.current_color_edit = (self.current_color_edit + 1) % self.settings_players
            
            # 閉じるボタン
            close_button = pygame.Rect(picker_x + picker_width // 2 - 50, picker_y + 320, 100, 40)
            if close_button.collidepoint(x, y):
                self.show_color_picker = False
                self.in_settings = True  # カラー選択画面を閉じたら設定画面に戻る
    def draw_game_board(self):
        """ゲームボードを描画する"""
        self.screen.fill(self.bg_color)  # 背景色を設定
        
        # ドットを描画
        for row in range(self.rows + 1):
            for col in range(self.cols + 1):
                x = self.margin + col * self.box_size
                y = self.margin + row * self.box_size
                pygame.draw.circle(self.screen, BLACK, (x, y), self.dot_size)
        
        # 水平線を描画
        for row in range(self.rows + 1):
            for col in range(self.cols):
                x1 = self.margin + col * self.box_size + self.dot_size
                y1 = self.margin + row * self.box_size
                x2 = self.margin + (col + 1) * self.box_size - self.dot_size
                y2 = y1
                
                if self.h_lines[row][col]:
                    # 既に引かれた線
                    # 線の色を所有者の色に設定
                    owner = self.h_line_owners[row][col]
                    if owner >= 0:
                        line_color = self.player_line_colors[owner]
                    else:
                        line_color = BLACK
                    
                    # 爆弾に隣接する線または完成したボックスの線は点滅させる
                    if self.is_line_flashing("h", row, col):
                        pygame.draw.line(self.screen, line_color, (x1, y1), (x2, y2), 5)
                    else:
                        pygame.draw.line(self.screen, line_color, (x1, y1), (x2, y2), 3)
                else:
                    # まだ引かれていない線
                    pygame.draw.line(self.screen, LIGHT_GRAY, (x1, y1), (x2, y2), 1)
        
        # 垂直線を描画
        for row in range(self.rows):
            for col in range(self.cols + 1):
                x1 = self.margin + col * self.box_size
                y1 = self.margin + row * self.box_size + self.dot_size
                x2 = x1
                y2 = self.margin + (row + 1) * self.box_size - self.dot_size
                
                if self.v_lines[row][col]:
                    # 既に引かれた線
                    # 線の色を所有者の色に設定
                    owner = self.v_line_owners[row][col]
                    if owner >= 0:
                        line_color = self.player_line_colors[owner]
                    else:
                        line_color = BLACK
                    
                    # 爆弾に隣接する線または完成したボックスの線は点滅させる
                    if self.is_line_flashing("v", row, col):
                        pygame.draw.line(self.screen, line_color, (x1, y1), (x2, y2), 5)
                    else:
                        pygame.draw.line(self.screen, line_color, (x1, y1), (x2, y2), 3)
                else:
                    # まだ引かれていない線
                    pygame.draw.line(self.screen, LIGHT_GRAY, (x1, y1), (x2, y2), 1)
    def draw_boxes(self):
        """ボックスを描画する"""
        for row in range(self.rows):
            for col in range(self.cols):
                if self.boxes[row][col] != -1:
                    # ボックスが所有されている場合
                    owner = self.boxes[row][col]
                    box_color = self.player_line_colors[owner]
                    
                    # ボックスの座標
                    x = self.margin + col * self.box_size + self.dot_size
                    y = self.margin + row * self.box_size + self.dot_size
                    width = self.box_size - 2 * self.dot_size
                    height = self.box_size - 2 * self.dot_size
                    
                    # グラデーション効果
                    if self.box_style['gradient']:
                        # 明るい色と暗い色を計算
                        r, g, b = box_color
                        light_color = (min(r + 50, 255), min(g + 50, 255), min(b + 50, 255))
                        dark_color = (max(r - 50, 0), max(g - 50, 0), max(b - 50, 0))
                        
                        # グラデーション効果のための複数の矩形を描画
                        steps = 5
                        for i in range(steps):
                            ratio = i / (steps - 1)
                            current_color = (
                                int(light_color[0] * (1 - ratio) + dark_color[0] * ratio),
                                int(light_color[1] * (1 - ratio) + dark_color[1] * ratio),
                                int(light_color[2] * (1 - ratio) + dark_color[2] * ratio)
                            )
                            
                            # 内側に向かって小さくなる矩形
                            inner_x = x + i * width / (2 * steps)
                            inner_y = y + i * height / (2 * steps)
                            inner_width = width - i * width / steps
                            inner_height = height - i * height / steps
                            
                            pygame.draw.rect(
                                self.screen, 
                                current_color, 
                                (inner_x, inner_y, inner_width, inner_height),
                                border_radius=self.box_style['border_radius']
                            )
                    else:
                        # 通常の塗りつぶし
                        pygame.draw.rect(
                            self.screen, 
                            box_color, 
                            (x, y, width, height),
                            border_radius=self.box_style['border_radius']
                        )
                    
                    # 爆発したボックスの場合、爆発エフェクトを描画
                    if (row, col) in self.exploded_boxes:
                        # 爆発エフェクト（十字の線）
                        center_x = x + width // 2
                        center_y = y + height // 2
                        explosion_size = min(width, height) // 2
                        
                        # 爆発の線を描画
                        for angle in range(0, 360, 45):
                            rad = math.radians(angle)
                            end_x = center_x + explosion_size * math.cos(rad)
                            end_y = center_y + explosion_size * math.sin(rad)
                            pygame.draw.line(self.screen, RED, (center_x, center_y), (end_x, end_y), 3)
                        
                        # 爆発の中心円
                        pygame.draw.circle(self.screen, ORANGE, (center_x, center_y), explosion_size // 3)
                
                # デバッグモードで爆弾を表示
                if self.show_mines and self.mines[row][col]:
                    # 爆弾の座標
                    x = self.margin + col * self.box_size + self.box_size // 2
                    y = self.margin + row * self.box_size + self.box_size // 2
                    
                    # 爆弾を描画 (黒ではなく暗い赤色で)
                    pygame.draw.circle(self.screen, (150, 0, 0), (x, y), self.box_size // 6)
                    # 導火線
                    pygame.draw.line(self.screen, DARK_GRAY, (x, y - self.box_size // 6), (x, y - self.box_size // 3), 3)
    
    def draw_player_info(self):
        """プレイヤー情報を描画する"""
        info_y = self.margin + (self.rows + 0.5) * self.box_size
        
        # 現在のプレイヤーを強調表示
        current_player_text = self.font.render(f"Player {self.current_player + 1}'s Turn", True, self.player_line_colors[self.current_player])
        self.screen.blit(current_player_text, (self.width // 2 - current_player_text.get_width() // 2, info_y))
        
        # 各プレイヤーのスコアを表示
        score_y = info_y + 40
        for i in range(self.player_count):
            # プレイヤーの色でスコアを表示
            if i < len(self.player_line_colors):
                player_color = self.player_line_colors[i]
            else:
                # 色が足りない場合はデフォルト色を使用
                player_color = PLAYER_COLORS[i % len(PLAYER_COLORS)]
                self.player_line_colors.append(player_color)
            
            # プレイヤーが爆発している場合は色を暗くする
            if not self.players_alive[i]:
                player_color = (player_color[0] // 2, player_color[1] // 2, player_color[2] // 2)
            
            player_text = self.small_font.render(f"Player {i + 1}: {self.player_scores[i]}", True, player_color)
            
            # プレイヤーが爆発している場合は「爆発」と表示
            if not self.players_alive[i]:
                player_text = self.small_font.render(f"Player {i + 1}: {self.player_scores[i]} (Exploded)", True, player_color)
            
            # プレイヤー情報を横に並べる
            x_pos = self.margin + i * (self.width - 2 * self.margin) // self.player_count
            self.screen.blit(player_text, (x_pos, score_y))
    
    def draw_game_over(self):
        """ゲーム終了画面を描画する"""
        if not self.game_over:
            return
        
        # 半透明のオーバーレイ
        overlay = pygame.Surface((self.width, self.height), pygame.SRCALPHA)
        overlay.fill((0, 0, 0, 128))  # 半透明の黒
        self.screen.blit(overlay, (0, 0))
        
        # ゲーム終了メッセージ
        game_over_text = self.font.render("Game Over", True, WHITE)
        self.screen.blit(game_over_text, (self.width // 2 - game_over_text.get_width() // 2, self.height // 2 - 60))
        
        # 勝者の表示
        if self.winner != -1:
            winner_text = self.font.render(f"Player {self.winner + 1} Wins!", True, self.player_line_colors[self.winner])
            self.screen.blit(winner_text, (self.width // 2 - winner_text.get_width() // 2, self.height // 2 - 20))
        else:
            tie_text = self.font.render("It's a Tie!", True, WHITE)
            self.screen.blit(tie_text, (self.width // 2 - tie_text.get_width() // 2, self.height // 2 - 20))
        
        # リスタート指示
        restart_text = self.small_font.render("Press R to Restart or ESC for Settings", True, WHITE)
        self.screen.blit(restart_text, (self.width // 2 - restart_text.get_width() // 2, self.height // 2 + 20))
    
    def start_game_with_settings(self):
        """設定画面の値でゲームを開始する"""
        try:
            # プレイヤーの色リストを確保
            player_colors = []
            for i in range(self.settings_players):
                if i < len(self.player_line_colors):
                    player_colors.append(self.player_line_colors[i])
                else:
                    player_colors.append(PLAYER_COLORS[i % len(PLAYER_COLORS)])
            
            settings = {
                'rows': self.settings_rows,
                'cols': self.settings_cols,
                'mine_count': self.settings_mines,
                'player_count': self.settings_players,
                'player_line_colors': player_colors,
                'box_size': self.box_size,
                'dot_size': self.dot_size,
                'margin': self.margin
            }
            self.__init__(settings)
            self.in_settings = False
        except Exception as e:
            print(f"ゲーム開始エラー: {e}")
            # エラーが発生しても設定画面を閉じる
            self.in_settings = False
    
    def run(self):
        """ゲームのメインループ"""
        running = True
        
        while running:
            try:
                for event in pygame.event.get():
                    if event.type == QUIT:
                        running = False
                    
                    # イベント処理の順序を変更
                    if self.show_color_picker:
                        self.handle_color_picker_events(event)
                    elif self.in_settings:
                        self.handle_settings_events(event)
                    else:
                        self.handle_game_events(event)
                
                # 画面の描画の順序も変更
                if self.show_color_picker:
                    self.draw_color_picker()
                elif self.in_settings:
                    self.draw_settings_screen()
                else:
                    self.draw_game_board()
                    self.draw_boxes()
                    self.draw_player_info()
                    self.draw_game_over()
                    self.update_flashing_lines()
                
                # 常に画面を更新
                pygame.display.flip()
                
                self.clock.tick(60)
            except Exception as e:
                print(f"エラーが発生しました: {e}")
                import traceback
                traceback.print_exc()
                # エラーが発生しても続行する
                continue
        
        pygame.quit()

# ゲームを実行
if __name__ == "__main__":
    game = MinedDotsAndBoxes()
    game.run()
