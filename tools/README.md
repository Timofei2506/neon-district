# Blender-пайплайн (вне Unity)

Требуется Blender 4.2+ (тестировалось на 4.2.9 LTS).

```
# машина/статика: метры, пивот в днище, длинная ось -> X, длина 5.2 м
blender -b --factory-startup --python export_unity_fbx.py -- in.fbx out.fbx car 5.2

# персонаж: арматура + анимация, без обёрток 0.01
blender -b --factory-startup --python export_unity_fbx.py -- in.fbx out.fbx character

# анализ городской модели -> JSON для трафика/мини-карты
blender -b --factory-startup --python analyze_city.py -- in.fbx layout.json

# разворот города под прямую сетку + экспорт
blender -b --factory-startup --python export_manhattan.py -- in.fbx out.fbx info.json 50
```
