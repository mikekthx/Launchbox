import os
import xml.etree.ElementTree as ET

resw_files = []
for root, dirs, files in os.walk('Strings'):
    for file in files:
        if file == 'Resources.resw':
            resw_files.append(os.path.join(root, file))

for file in resw_files:
    tree = ET.parse(file)
    root = tree.getroot()

    # Check if they exist to avoid duplicates
    has_collapsed = False
    has_expanded = False
    for data in root.findall('data'):
        if data.attrib.get('name') == 'CollapseState_Collapsed':
            has_collapsed = True
        if data.attrib.get('name') == 'CollapseState_Expanded':
            has_expanded = True

    if not has_collapsed:
        elem = ET.Element('data', {'name': 'CollapseState_Collapsed', 'xml:space': 'preserve'})
        val = ET.SubElement(elem, 'value')

        # Translate based on folder if needed, but for now just put english strings since it's just for screen readers and it's better than nothing, or ask to just use English
        if 'de' in file: val.text = 'Reduziert'
        elif 'ko' in file: val.text = '축소됨'
        elif 'fr' in file: val.text = 'Réduit'
        elif 'ja' in file: val.text = '折りたたまれています'
        elif 'zh-Hans' in file: val.text = '已折叠'
        elif 'it' in file: val.text = 'Compresso'
        elif 'pt-BR' in file: val.text = 'Recolhido'
        elif 'tr' in file: val.text = 'Daraltılmış'
        elif 'es' in file: val.text = 'Contraído'
        elif 'ru' in file: val.text = 'Свернуто'
        elif 'zh-Hant' in file: val.text = '已摺疊'
        elif 'pl' in file: val.text = 'Zwinięte'
        else: val.text = 'Collapsed'
        root.append(elem)

    if not has_expanded:
        elem = ET.Element('data', {'name': 'CollapseState_Expanded', 'xml:space': 'preserve'})
        val = ET.SubElement(elem, 'value')
        if 'de' in file: val.text = 'Erweitert'
        elif 'ko' in file: val.text = '확장됨'
        elif 'fr' in file: val.text = 'Développé'
        elif 'ja' in file: val.text = '展開されています'
        elif 'zh-Hans' in file: val.text = '已展开'
        elif 'it' in file: val.text = 'Espanso'
        elif 'pt-BR' in file: val.text = 'Expandido'
        elif 'tr' in file: val.text = 'Genişletilmiş'
        elif 'es' in file: val.text = 'Expandido'
        elif 'ru' in file: val.text = 'Развернуто'
        elif 'zh-Hant' in file: val.text = '已展開'
        elif 'pl' in file: val.text = 'Rozwinięte'
        else: val.text = 'Expanded'
        root.append(elem)

    tree.write(file, encoding='utf-8', xml_declaration=True)

print("Added localized strings.")
