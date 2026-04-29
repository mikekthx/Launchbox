import os

locales = {
    'de': ('Reduziert', 'Erweitert'),
    'ko': ('축소됨', '확장됨'),
    'fr': ('Réduit', 'Développé'),
    'ja': ('折りたたまれています', '展開されています'),
    'zh-Hans': ('已折叠', '已展开'),
    'it': ('Compresso', 'Espanso'),
    'pt-BR': ('Recolhido', 'Expandido'),
    'tr': ('Daraltılmış', 'Genişletilmiş'),
    'es': ('Contraído', 'Expandido'),
    'ru': ('Свернуто', 'Развернуто'),
    'zh-Hant': ('已摺疊', '已展開'),
    'pl': ('Zwinięte', 'Rozwinięte'),
    'en-US': ('Collapsed', 'Expanded')
}

resw_files = []
for root, dirs, files in os.walk('Strings'):
    for file in files:
        if file == 'Resources.resw':
            resw_files.append(os.path.join(root, file))

for file in resw_files:
    locale = file.split(os.sep)[1]

    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    if "CollapseState_Collapsed" in content:
        continue

    collapsed, expanded = locales.get(locale, ('Collapsed', 'Expanded'))

    new_data = f"""  <data name="CollapseState_Collapsed" xml:space="preserve">
    <value>{collapsed}</value>
  </data>
  <data name="CollapseState_Expanded" xml:space="preserve">
    <value>{expanded}</value>
  </data>
</root>"""

    content = content.replace("</root>", new_data)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)

print("Fixed resw files")
