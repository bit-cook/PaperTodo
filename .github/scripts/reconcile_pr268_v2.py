from pathlib import Path

source = Path('.github/scripts/reconcile_pr268.py').read_text(encoding='utf-8')
source = source.replace(
    '''    "Four levels (Plain text / Basic / Enhanced / Full Render). With **Full Render**,
",''',
    '''    "Four levels (Plain text / Basic / Enhanced / Full Render). With **Full Render**,",''',
)
source = source.replace(
    '''    "Three levels (Plain text / Basic / Full Render). **Basic** uses the former Enhanced presentation, keeping Markdown markers while fading syntax and showing bullets and dividers. With **Full Render**,
",''',
    '''    "Three levels (Plain text / Basic / Full Render). **Basic** uses the former Enhanced presentation, keeping Markdown markers while fading syntax and showing bullets and dividers. With **Full Render**,",''',
)
exec(compile(source, 'reconcile_pr268_fixed.py', 'exec'), {'__name__': '__main__'})
