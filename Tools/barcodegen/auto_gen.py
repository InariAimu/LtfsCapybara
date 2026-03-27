import os, sys
import shlex

with open('labels.txt', 'r') as f:
    for line in f:
        line = line.strip()
        if line:
            cmd = f'{sys.executable} generate_lto_label.py {shlex.quote(line)}'
            os.system(cmd)

