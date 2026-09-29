#!/usr/bin/env python3
"""Syntax and static contract checks only; emphatically NOT a C# build or SQL check."""
from pathlib import Path
import sys, subprocess, json, xml.etree.ElementTree as ET
import tree_sitter as ts, tree_sitter_c_sharp as cs
root=Path(__file__).resolve().parents[1];p=ts.Parser(ts.Language(cs.language()));bad=[]
files=list(root.glob('Andalos.API*/**/*.cs'))+list(root.glob('tools/Authorization.Bench/**/*.cs'))
for f in files:
 if any(part in ('bin','obj') for part in f.parts): continue
 tree=p.parse(f.read_bytes())
 if tree.root_node.has_error:bad.append(str(f.relative_to(root)))
assert not bad, 'C# syntax errors: '+str(bad)
for f in root.glob('Andalos.API*/**/*.csproj'):ET.parse(f)
for f in (root/'docs/authorization').glob('*.json'):json.loads(f.read_text())
subprocess.run([sys.executable,str(root/'tools/authorization_inventory.py'),'--check'],cwd=root,check=True)
snapshot=(root/'Andalos.API/Migrations/AppDbContextModelSnapshot.cs').read_text()
designer=(root/'Andalos.API/Migrations/20260929190000_DetailedAuthorization.Designer.cs').read_text()
a=snapshot[snapshot.index('            modelBuilder'):snapshot.index('#pragma warning restore')]
b=designer[designer.index('            modelBuilder'):designer.index('#pragma warning restore')]
assert a==b, 'designer/snapshot target body differs'
assert '[Migration("20260929190000_DetailedAuthorization")]' in designer and '[DbContext(typeof(AppDbContext))]' in designer
print(f'{len(files)} C# files parsed without syntax errors; frozen target parity and JSON/XML OK. No semantic compilation performed.')
