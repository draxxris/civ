# Fonts

`NotoSerif-VariableFont.ttf` is the UI font. It is Noto Serif from the Noto Project,
renamed from `NotoSerif-VariableFont_wdth,wght.ttf`. `NotoSerif-Italic-VariableFont.ttf`
is its italic, renamed from `NotoSerif-Italic-VariableFont_wdth,wght.ttf`. The SIL Open
Font License 1.1 in `OFL.txt` applies to both.

Each file is a variable font with weight (`wght`) and width (`wdth`) axes. `BrassSkin`
loads them and uses a `FontVariation` to select weight 400 and width 100. The upright
font is the default font of the theme. Use `BrassSkin.SerifItalicFont()` to set the
italic on a control.
