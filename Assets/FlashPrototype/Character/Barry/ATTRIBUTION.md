# Barry Allen (out of costume) attribution

**Body:** the user-supplied `male-skinny-base-mesh.zip` (Sketchfab download layout: `source/Male_07.zip`,
containing `Male_07.obj`), supplied on 2026-10-05. The archive carries no author or licence information; none is
asserted here. The mesh was reshaped toward an athletic build (fuller thighs,
calves, arms, deltoids and neck; `Tools/Blender/shape_body.py`), scaled to 1.83 m, rigged with a Mixamo-named
skeleton and skinned (`Tools/Blender/rig_tpose_mesh.py`); its eyelash shells were removed. The same body, with generic
painted faces, is the Townsperson model for the city's people.

**Design and face:** the user's own Meshy model “Plaid T-Pose Man” (https://www.meshy.ai/s/XY6tNX; the share
page shows CC BY 4.0) and the reference renders the user supplied. The `.meshy` file is encrypted and is not used.
The face texture is the user's front render warped onto the base face through matching landmarks, with its
lighting evened out; clothing (plaid overshirt, tee, jeans, sneakers, watch) and hair were modelled from the
base body and painted procedurally to match the renders (`Tools/Blender/build_csi_barry.py`,
`Tools/csi_textures.py`).

Barry Allen is a DC character. This is a fan prototype asset; no endorsement is implied.
