#!/usr/bin/env python3
import os

# Set environment variable for AVX2 detection
os.environ['PYGAME_DETECT_AVX2'] = '1'

# Import the game
from mine_dots_and_boxes import MinedDotsAndBoxes

# Run the game
if __name__ == "__main__":
    game = MinedDotsAndBoxes()
    game.run()
