import os, sys

with open('labels.txt', 'r') as f:
    for line in f:
        line = line.strip()
        if line:
            os.system(f'python generate_lto_label.py {line}')

