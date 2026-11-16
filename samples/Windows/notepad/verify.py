# Copyright Xeno Innovations, Inc. 2026
# See LICENSE in the repository root.
"""Windows-only integration check for the Section 4 Notepad source listings.

Run from any directory: python samples/Windows/notepad/verify.py
Use --compiler to select a different native zero.exe. Interactive file dialogs
and prompts are stubbed; their behavior is covered by Chapter 4.5's manual checks.
"""
from pathlib import Path
import argparse
import subprocess
import sys

if sys.platform != 'win32':
    raise SystemExit('This runtime check requires Windows.')
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--compiler', type=Path)
args = parser.parse_args()

root=Path(__file__).resolve().parents[3]
compiler=(args.compiler or root/'publish/zero.exe').resolve()
if not compiler.is_file():
    raise SystemExit(f'Compiler not found: {compiler}. Build it or pass --compiler.')
out=root/'output/notepad-check'
out.mkdir(parents=True, exist_ok=True)
# Keep every printed listing identical to its runnable sample and compile each stage.
import re
stages = [
    ('notepad-01-window', 'zasm-04-02-notepad-create-the-window'),
    ('notepad-02-editor', 'zasm-04-03-notepad-add-the-text-editor'),
    ('notepad-03-complete', 'zasm-04-04-notepad-open-and-save-files'),
]
for stem, chapter in stages:
    src = root/'samples/Windows/notepad'/f'{stem}.zasm'
    listing = (root/'docs/zasm'/f'{chapter}.md').read_text(encoding='utf-8')
    blocks = re.findall(r'```asm\n(.*?)```', listing, re.S)
    assert blocks == [src.read_text(encoding='utf-8')], f'Listing differs: {chapter}'
    result = subprocess.run([str(compiler), str(src), str(out/f'{stem}.exe')],
                            capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
print('PASS: All three complete chapter listings match their standalone sources and compile.')
sample=(root/'samples/Windows/win-ui-10-notepad.zasm').read_text(encoding='utf-8')
assert sample == (root/'samples/Windows/notepad/notepad-03-complete.zasm').read_text(encoding='utf-8'), 'Finished sample differs from the final tutorial stage.'
# Exercise the exact I/O routines against a hidden EDIT control. Replace only
# interactive filename selection and error popups with deterministic test stubs.
assert 'jmp start' in sample
sample=sample.replace('jmp start','jmp test_start',1)
start=sample.index('choose_path:\n')
end=sample.index('open_document:\n',start)
sample=sample[:start]+'choose_path:\n  mov eax, 1\n  ret\n\n'+sample[end:]
sample=sample[:sample.index('show_error:\n')]+'show_error:\n  xor eax, eax\n  ret\n'
sample+='#IMPORT msvcrt.dll, strcmp\n'
paths={}
fixtures={'input':b'Loaded text\r\nSecond line', 'empty':b'',
          'maximum':b'A'*65535, 'oversized':b'B'*65536, 'nul':b'one\x00two'}
for key,data in fixtures.items():
    path=out/f'{key}.txt'
    path.write_bytes(data)
    paths[key]=path
paths['saved']=out/'saved.txt'
paths['empty_saved']=out/'empty-saved.txt'
paths['bad']=out/'directory-that-does-not-exist'/'bad.txt'
for key,path in paths.items():
    sample+=f"test_path_{key} db '{path.as_posix()}', 0\n"
sample+="test_initial db 'Initial text', 13, 10, 'Second line', 0\n"
sample+="test_revised db 'Revised text', 13, 10, 'Second line', 0\n"
sample+="test_loaded db 'Loaded text', 13, 10, 'Second line', 0\n"
sample+="test_parent_class db 'STATIC', 0\n"
sample+='''
test_start:
  lea ebp, [esp]
  push 66080
  push 0x40
  call [GlobalAlloc]
  test eax, eax
  jz test_fail
  push eax
  pop esi
  push 0
  push 0
  push 0
  push 0
  push 100
  push 100
  push 0
  push 0
  push 0x80000000
  push empty_text
  push test_parent_class
  push 0
  call [CreateWindowExA]
  test eax, eax
  jz test_fail
  push eax
  pop ebx
  push 0
  push 0
  push 201
  push ebx
  push 100
  push 100
  push 0
  push 0
  push 0x40B110C4
  push empty_text
  push edit_class
  push 0
  call [CreateWindowExA]
  test eax, eax
  jz test_fail
  lea edx, [esi]
  call store_dword
  push 0
  push 65535
  push 0xC5
  mov eax, [esi]
  push eax
  call [SendMessageA]
'''

def pending(key):
    return f'''  push test_path_{key}
  lea eax, [esi + 268]
  push eax
  call [strcpy]
  add esp, 8
'''

def text(key):
    return f'''  push test_{key}
  mov eax, [esi]
  push eax
  call [SetWindowTextA]
  test eax, eax
  jz test_fail
'''

def check(result):
    return f'''  cmp eax, {result}
  jnz test_fail
  cmp esp, ebp
  jnz test_fail
'''

def clean():
    return '''  xor eax, eax
  lea edx, [esi + 4]
  call store_dword
'''

sample+=text('initial')+pending('saved')+'  call save_as_document\n'+check(1)
sample+=text('revised')+'''  mov eax, 1
  lea edx, [esi + 4]
  call store_dword
  call save_document
'''+check(1)
sample+='''  mov eax, [esi + 4]
  test eax, eax
  jnz test_fail
'''
sample+=pending('input')+'  call open_document\n'+check(1)
sample+='''  push 65536
  lea eax, [esi + 528]
  push eax
  mov eax, [esi]
  push eax
  call [GetWindowTextA]
  push test_loaded
  lea eax, [esi + 528]
  push eax
  call [strcmp]
  add esp, 8
'''+check(0)
for key in ['maximum','oversized','nul','empty']:
    sample+=clean()+pending(key)+'  call open_document\n'+check(0 if key in ['oversized','nul'] else 1)
    if key in ['oversized','nul']:
        # Rejected reads must leave the previously loaded 65535-byte editor intact.
        sample+='  mov eax, [esi]\n  push eax\n  call [GetWindowTextLengthA]\n'+check(65535)
sample+=pending('empty_saved')+'  call save_as_document\n'+check(1)
sample+=pending('bad')+'''  mov eax, 1
  lea edx, [esi + 4]
  call store_dword
  call write_pending
'''+check(0)
sample+='''  mov eax, [esi + 4]
'''+check(1)
sample+='''  push ebx
  call [DestroyWindow]
  push esi
  call [GlobalFree]
  push 0
  call [ExitProcess]
test_fail:
  push 99
  call [ExitProcess]
'''
branches=[]
def label_failure(match):
    branches.append(sample[max(0,match.start()-180):match.end()])
    return match.group(1)+f' test_failure_{len(branches)}'
sample=re.sub(r'(jz|jnz) test_fail\b',label_failure,sample)
for i in range(1,len(branches)+1):
    sample+=f'\ntest_failure_{i}:\n  push {i}\n  call [ExitProcess]\n'
src=out/'runtime-check.zasm'
src.write_text(sample,encoding='utf-8')
exe=src.with_suffix('.exe')
compile=subprocess.run([str(compiler),str(src),str(exe)],capture_output=True,text=True)
assert compile.returncode==0,compile.stdout+compile.stderr
run=subprocess.run([str(exe)],timeout=20)
assert run.returncode==0,f'Runtime check failed with exit code {run.returncode}:\n'+(branches[run.returncode-1] if 0<run.returncode<=len(branches) else '')
assert paths['saved'].read_bytes()==b'Revised text\r\nSecond line'
assert paths['empty_saved'].read_bytes()==b''
print('PASS: Save As, Save current path, Open, exact text bytes, empty files, 65535-byte boundary, oversized/NUL rejection, failed-write dirty state, and stack balance.')
