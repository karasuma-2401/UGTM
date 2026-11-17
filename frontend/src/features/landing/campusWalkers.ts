import * as THREE from 'three';
import { visitorSeed, walkingPose } from './campusJourney';

const shirts = ['#2054a5', '#e57c43', '#458270', '#8262a3', '#d2aa49'];

// Shared instanced geometry keeps a real visitor per figure without a draw call per person.
export function createCampusWalkers(scene: THREE.Scene) {
  const geometry = {
    body: new THREE.BoxGeometry(0.57, 0.68, 0.34),
    head: new THREE.SphereGeometry(0.22, 8, 6),
    hair: new THREE.SphereGeometry(0.225, 8, 6, 0, Math.PI * 2, 0, Math.PI * 0.5),
    limb: new THREE.BoxGeometry(0.17, 0.72, 0.19),
    arm: new THREE.BoxGeometry(0.16, 0.62, 0.18),
  };
  const materials = {
    shirt: new THREE.MeshStandardMaterial({ roughness: 0.9 }),
    skin: new THREE.MeshStandardMaterial({ color: '#d6a17c', roughness: 0.9 }),
    hair: new THREE.MeshStandardMaterial({ color: '#293644', roughness: 1 }),
    pants: new THREE.MeshStandardMaterial({ color: '#30465e', roughness: 1 }),
  };
  let ids: string[] = [];
  let seeds: number[] = [];
  let meshes: THREE.InstancedMesh[] = [];
  const root = new THREE.Matrix4();
  const local = new THREE.Matrix4();
  const rotation = new THREE.Matrix4();
  const matrix = new THREE.Matrix4();
  const position = new THREE.Vector3();
  const quaternion = new THREE.Quaternion();
  const up = new THREE.Vector3(0, 1, 0);
  const scale = new THREE.Vector3(1.2, 1.2, 1.2);
  const color = new THREE.Color();

  function clearMeshes() {
    for (const mesh of meshes) {
      scene.remove(mesh);
      mesh.dispose();
    }
    meshes = [];
  }
  function add(shape: THREE.BufferGeometry, material: THREE.Material, count: number) {
    const mesh = new THREE.InstancedMesh(shape, material, count);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    mesh.castShadow = true;
    // Figures move around several paths, so a stale initial bounding sphere is unsuitable.
    mesh.frustumCulled = false;
    scene.add(mesh);
    return mesh;
  }
  function part(
    mesh: THREE.InstancedMesh,
    index: number,
    x: number,
    y: number,
    z: number,
    angle = 0,
    drop = 0,
  ) {
    local.makeTranslation(x, y, z);
    if (angle || drop) {
      rotation.makeRotationX(angle);
      local.multiply(rotation);
      rotation.makeTranslation(0, -drop, 0);
      local.multiply(rotation);
    }
    matrix.multiplyMatrices(root, local);
    mesh.setMatrixAt(index, matrix);
  }
  return {
    get count() {
      return ids.length;
    },
    setVisitors(visitors: string[]) {
      if (ids.length === visitors.length && ids.every((id, index) => id === visitors[index]))
        return;
      clearMeshes();
      ids = [...visitors];
      seeds = ids.map(visitorSeed);
      if (!ids.length) return;
      meshes = [
        add(geometry.body, materials.shirt, ids.length),
        add(geometry.head, materials.skin, ids.length),
        add(geometry.hair, materials.hair, ids.length),
        add(geometry.limb, materials.pants, ids.length * 2),
        add(geometry.arm, materials.shirt, ids.length * 2),
      ];
      seeds.forEach((seed, index) => {
        color.set(shirts[seed % shirts.length]);
        meshes[0].setColorAt(index, color);
        meshes[4].setColorAt(index * 2, color);
        meshes[4].setColorAt(index * 2 + 1, color);
      });
    },
    update(time: number, animate: boolean) {
      if (!meshes.length) return;
      seeds.forEach((seed, index) => {
        const pose = walkingPose(seed, time);
        position.set(pose.x, 0.25 + (animate ? Math.abs(pose.stride) * 0.06 : 0), pose.z);
        quaternion.setFromAxisAngle(up, pose.heading);
        root.compose(position, quaternion, scale);
        part(meshes[0], index, 0, 1.2, 0);
        part(meshes[1], index, 0, 1.78, 0);
        part(meshes[2], index, 0, 1.8, 0);
        const stride = animate ? pose.stride : 0;
        for (let side = 0; side < 2; side++) {
          const sign = side ? 1 : -1;
          part(meshes[3], index * 2 + side, sign * 0.16, 0.89, 0, stride * sign, 0.36);
          part(meshes[4], index * 2 + side, sign * 0.38, 1.48, 0, -stride * sign, 0.27);
        }
      });
      for (const mesh of meshes) mesh.instanceMatrix.needsUpdate = true;
    },
    dispose() {
      clearMeshes();
      Object.values(geometry).forEach((value) => value.dispose());
      Object.values(materials).forEach((value) => value.dispose());
    },
  };
}
