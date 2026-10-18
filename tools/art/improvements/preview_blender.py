import bpy, sys, math
argv = sys.argv[sys.argv.index("--")+1:]
src, out = argv[0], argv[1]
yaw = float(argv[2]) if len(argv) > 2 else 0
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
# hex ground (Blender Z-up; glTF -Z forward -> Blender +Y)
import bmesh
me = bpy.data.meshes.new("hex"); bm = bmesh.new()
vs = [bm.verts.new((math.cos(math.radians(30+60*i)), math.sin(math.radians(30+60*i)), 0)) for i in range(6)]
bm.faces.new(vs); bm.to_mesh(me)
ob = bpy.data.objects.new("hex", me); bpy.context.scene.collection.objects.link(ob)
mat = bpy.data.materials.new("g"); mat.diffuse_color = (0.25, 0.4, 0.15, 1); me.materials.append(mat)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'VERTEX'
sc.render.resolution_x = 900; sc.render.resolution_y = 700
cam = bpy.data.cameras.new("c"); cam.lens = 50
co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
pitch = math.radians(40); d = float(argv[3]) if len(argv) > 3 else 1.3; y = math.radians(yaw)
co.location = (d*math.cos(pitch)*math.sin(y), -d*math.cos(pitch)*math.cos(y), d*math.sin(pitch))
co.rotation_euler = (math.pi/2 - pitch, 0, y)
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
