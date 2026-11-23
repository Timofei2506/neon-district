"""
Анализ городской модели: угол сетки улиц, границы, свободные коридоры (улицы),
плотность застройки. Результат — JSON для Unity (трафик, пешеходы, мини-карта).
"""
import bpy, sys, json, math
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--')+1:]
src, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.endswith('.fbx'):
    bpy.ops.import_scene.fbx(filepath=src)
else:
    bpy.ops.import_scene.gltf(filepath=src)

dg = bpy.context.evaluated_depsgraph_get()
pts = []
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    mw = o.matrix_world
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    # добавляем центроиды треугольников: так застройка покрывается плотно даже в мелких моделях
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    if nt > 0:
        tri = np.empty(nt * 3, dtype=np.int32)
        me.loop_triangles.foreach_get('vertices', tri)
        tri = tri.reshape(-1, 3)
        cent = co[tri].mean(axis=1)
        co = np.vstack([co, cent])
    # в мировые координаты
    M = np.array(mw)
    co = co @ M[:3, :3].T + M[:3, 3]
    pts.append(co)
    ev.to_mesh_clear()

P = np.vstack(pts)
print("points:", len(P))
mn = P.min(axis=0); mx = P.max(axis=0)
size = mx - mn
print("bounds", mn.round(1), mx.round(1), "size", size.round(1))

# --- доминирующий угол сетки: гистограмма направлений рёбер по высоте > 4 м
XZ = P[:, [0, 1]]
# берём «стены»: точки выше 6 м
wall = XZ[P[:, 2] > 6.0]
print("wall points:", len(wall))

# оценка угла: перебираем поворот и считаем «резкость» гистограммы по X и Y
best_a, best_score = 0.0, -1.0
sample = wall[::max(1, len(wall)//60000)]
for deg in range(0, 91, 1):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    x = sample[:, 0] * c - sample[:, 1] * s
    y = sample[:, 0] * s + sample[:, 1] * c
    hx, _ = np.histogram(x, bins=280)
    hy, _ = np.histogram(y, bins=280)
    score = (hx.astype(float)**2).sum() + (hy.astype(float)**2).sum()
    if score > best_score:
        best_score, best_a = score, deg
print("grid angle (deg):", best_a)

a = math.radians(best_a)
c, s = math.cos(a), math.sin(a)
Xr = XZ[:, 0] * c - XZ[:, 1] * s
Yr = XZ[:, 0] * s + XZ[:, 1] * c

# --- карта занятости (здания выше 3 м), шаг 4 м
cell = 8.0
xr_min, yr_min = Xr.min(), Yr.min()
nx = int((Xr.max() - xr_min) / cell) + 1
ny = int((Yr.max() - yr_min) / cell) + 1
occ = np.zeros((nx, ny), dtype=bool)
high = P[:, 2] > 3.0
ix = ((Xr[high] - xr_min) / cell).astype(int)
iy = ((Yr[high] - yr_min) / cell).astype(int)
ix = np.clip(ix, 0, nx-1); iy = np.clip(iy, 0, ny-1)
occ[ix, iy] = True
print("occupancy", occ.shape, "built ratio", round(occ.mean(), 3))

# --- улицы: линии сетки, где мало застройки
col_free = 1.0 - occ.mean(axis=1)   # по X
row_free = 1.0 - occ.mean(axis=0)   # по Y
def peaks(arr, alpha=1.6, min_gap=3):
    # адаптивный порог: доля от максимума (сетка не идеальна — углы, дворы, парки)
    thresh = max(0.30, float(arr.max()) * alpha * 0.55)
    idx = np.where(arr > thresh)[0]
    groups = []
    cur = []
    for i in idx:
        if cur and i - cur[-1] > 2:
            groups.append(cur); cur = []
        cur.append(i)
    if cur: groups.append(cur)
    centers = [int(np.mean(g)) for g in groups if len(g) >= 1]
    merged = []
    for c2 in centers:
        if not merged or c2 - merged[-1] >= min_gap:
            merged.append(c2)
    return merged
sx = peaks(col_free)
sy = peaks(row_free)
print("free profile max X:", round(float(col_free.max()),3), "max Z:", round(float(row_free.max()),3))
print("street lines X:", len(sx), "Z:", len(sy))

streets_x = [round(xr_min + i * cell, 1) for i in sx]
streets_z = [round(yr_min + i * cell, 1) for i in sy]

data = {
    "source": src.split('/')[-1],
    "angle_deg": best_a,
    "bounds_min": [round(float(v), 1) for v in mn],
    "bounds_max": [round(float(v), 1) for v in mx],
    "size": [round(float(v), 1) for v in size],
    "cell": cell,
    "streets_x": streets_x,
    "streets_z": streets_z,
    "built_ratio": round(float(occ.mean()), 3),
    # карта занятости в RLE по строкам — компактно и удобно для мини-карты
    "occupancy": {
        "nx": int(nx), "ny": int(ny), "cell": cell,
        "x0": round(float(xr_min), 1), "y0": round(float(yr_min), 1),
        "rows": [int(v) for v in np.packbits(occ, axis=1).flatten()],
        "row_bytes": int((ny + 7) // 8),
    },
}
with open(out, 'w') as f:
    json.dump(data, f)
print("CITY_JSON_DONE", out, "streets:", len(streets_x), "x", len(streets_z))
