#!/usr/bin/env python3
import os
import sys

# Set environment variable for AVX2 detection
os.environ['PYGAME_DETECT_AVX2'] = '1'

# Import the game
from mine_dots_and_boxes import MinedDotsAndBoxes

# Debug function to print events
def print_events(events):
    for event in events:
        print(f"Event: {event}")

# Run the game with debug
if __name__ == "__main__":
    game = MinedDotsAndBoxes()
    
    # Debug info
    print("Initial settings:")
    print(f"show_color_picker: {game.show_color_picker}")
    print(f"in_settings: {game.in_settings}")
    
    # Override run method to add debug
    original_run = game.run
    
    def debug_run():
        running = True
        
        while running:
            try:
                # Get events but don't process them yet
                events = list(pygame.event.get())
                
                # Debug info for mouse clicks
                for event in events:
                    if event.type == pygame.MOUSEBUTTONDOWN:
                        x, y = event.pos
                        print(f"Mouse click at ({x}, {y}), button: {event.button}")
                        print(f"Current state - in_settings: {game.in_settings}, show_color_picker: {game.show_color_picker}")
                        
                        # If in settings screen, check if color button was clicked
                        if game.in_settings:
                            settings_width = 500
                            settings_height = 400
                            settings_x = game.width // 2 - settings_width // 2
                            settings_y = game.height // 2 - settings_height // 2
                            
                            # Check if color button area was clicked
                            if settings_x + 150 <= x <= settings_x + 350 and settings_y + 300 <= y <= settings_y + 330:
                                print("Color button area was clicked!")
                
                # Process events
                for event in events:
                    if event.type == pygame.QUIT:
                        running = False
                    
                    if game.in_settings:
                        game.handle_settings_events(event)
                    elif game.show_color_picker:
                        game.handle_color_picker_events(event)
                    else:
                        game.handle_game_events(event)
                
                # After processing events, check state again
                if any(event.type == pygame.MOUSEBUTTONDOWN for event in events):
                    print(f"After processing - in_settings: {game.in_settings}, show_color_picker: {game.show_color_picker}")
                
                # Draw screen
                if game.in_settings:
                    game.draw_settings_screen()
                elif game.show_color_picker:
                    game.draw_color_picker()
                else:
                    game.draw_game_board()
                    game.draw_boxes()
                    game.draw_player_info()
                    game.draw_game_over()
                    game.update_flashing_lines()
                    pygame.display.flip()
                
                game.clock.tick(60)
            except Exception as e:
                print(f"Error: {e}")
                import traceback
                traceback.print_exc()
                continue
        
        pygame.quit()
    
    # Replace run method
    import pygame
    game.run = debug_run
    
    # Run the game
    game.run()
