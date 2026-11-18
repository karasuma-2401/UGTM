import { Box3, MathUtils, Vector3 } from 'three';
import type { Object3D, PerspectiveCamera } from 'three';

export const CAMPUS_OVERVIEW_FOV = 34;

export function campusFramePoints(model: Object3D) {
  const points: Vector3[] = [];
  model.updateMatrixWorld(true);
  model.traverse((object) => {
    if (!('isMesh' in object) || !object.isMesh) return;
    const box = new Box3().setFromObject(object, true);
    for (const x of [box.min.x, box.max.x])
      for (const y of [box.min.y, box.max.y])
        for (const z of [box.min.z, box.max.z]) points.push(new Vector3(x, y, z));
  });
  return points;
}

// Extend the canvas at the original pixel scale instead of backing the camera
// away. Only a physical viewport narrower than the map needs responsive scaling.
export function frameCampusOverview(
  camera: PerspectiveCamera,
  points: Vector3[],
  width: number,
  height: number,
  availableWidth: number,
) {
  camera.clearViewOffset();
  camera.aspect = width / height;
  const tan = Math.tan(MathUtils.degToRad(CAMPUS_OVERVIEW_FOV) / 2);
  let scale = Math.max(1, 1.3 / camera.aspect);
  const projected = new Vector3();
  let minX = 0,
    maxX = 0,
    minY = 0,
    maxY = 0;
  function measure() {
    camera.fov = MathUtils.radToDeg(2 * Math.atan(tan * scale));
    camera.updateProjectionMatrix();
    camera.updateMatrixWorld();
    minX = minY = Infinity;
    maxX = maxY = -Infinity;
    for (const point of points) {
      projected.copy(point).project(camera);
      minX = Math.min(minX, (projected.x * width) / 2);
      maxX = Math.max(maxX, (projected.x * width) / 2);
      minY = Math.min(minY, (-projected.y * height) / 2);
      maxY = Math.max(maxY, (-projected.y * height) / 2);
    }
  }
  if (!points.length) return { width, height };
  measure();
  const requiredWidth = (maxX - minX) * 1.04;
  if (requiredWidth > availableWidth) {
    scale *= requiredWidth / availableWidth;
    measure();
  }
  const frameWidth = Math.max(width, Math.ceil((maxX - minX) * 1.04));
  const verticalPadding = Math.max(12, (maxY - minY) * 0.02);
  const frameHeight = Math.max(height, Math.ceil(maxY - minY + verticalPadding * 2));
  camera.setViewOffset(
    width,
    height,
    width / 2 + (minX + maxX) / 2 - frameWidth / 2,
    // Anchor the ground just above the controls. Overscan grows upward in paint
    // space, so orbiting never changes the hero's layout or leaves a large gap.
    height / 2 + maxY - frameHeight + verticalPadding,
    frameWidth,
    frameHeight,
  );
  return { width: frameWidth, height: frameHeight };
}
