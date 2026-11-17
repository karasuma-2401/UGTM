import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const source = readFileSync(
  new URL('../src/features/landing/campusJourney.ts', import.meta.url),
  'utf8',
);
const { outputText } = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
});
const journey = {};
new Function('exports', outputText)(journey);

test('tour completes in 20 seconds and covers all sides of the map', () => {
  const { campusViews: views, TOUR_DURATION_MS: duration } = journey;
  assert.equal(views.length, 10);
  assert.equal(duration, 20000);
  assert.ok(views.some(({ position: [x] }) => x < -100));
  assert.ok(views.some(({ position: [x] }) => x > 100));
  assert.ok(views.some(({ position: [, , z] }) => z < -100));
  assert.ok(views.some(({ position: [, , z] }) => z > 100));
  assert.ok(views.some(({ position: [, y] }) => y > 150));
});

test('reload and reset start outside gate A, looking into the campus from the west', () => {
  const first = journey.campusViews[0];
  assert.equal(first.name, 'Cổng A');
  assert.ok(first.position[0] < -100);
  assert.ok(Math.abs(first.position[0]) > Math.abs(first.position[2]) * 2);
  assert.ok(Math.abs(first.target[0]) < 15 && Math.abs(first.target[2]) < 15);
});

const tourSource = readFileSync(
  new URL('../src/features/landing/campusTour.ts', import.meta.url),
  'utf8',
);
const tourCode = ts.transpileModule(tourSource, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const tourModule = {};
new Function('require', 'exports', tourCode)((name) => {
  if (name === 'three') return THREE;
  if (name === './campusJourney') return journey;
  throw new Error(`Unexpected module: ${name}`);
}, tourModule);

test('cinematic flight goes through the campus and keeps a smooth closed seam', () => {
  const tour = tourModule.createCampusTour();
  const target = new THREE.Vector3();
  const epsilon = 0.00001;
  const total = journey.campusFlightBeats.reduce((sum, beat) => sum + beat, 0);
  assert.equal(journey.campusFlightBeats.length, journey.campusFlight.length);
  assert.ok(Math.min(...journey.campusFlightBeats) > 0);
  assert.ok(Math.max(...journey.campusFlightBeats) / Math.min(...journey.campusFlightBeats) > 2);
  for (const axis of [0, 2]) {
    assert.ok(journey.campusFlight.some(({ position }) => position[axis] > 100));
    assert.ok(journey.campusFlight.some(({ position }) => position[axis] < -100));
  }
  let elapsed = 0;
  const at = (phase) => {
    const position = new THREE.Vector3();
    tour.sample(phase, position, target);
    return position;
  };
  for (let index = 0; index < journey.campusFlight.length; index++) {
    const phase = elapsed / total;
    elapsed += journey.campusFlightBeats[index];
    const position = at(phase);
    assert.ok(
      position.distanceTo(new THREE.Vector3(...journey.campusFlight[index].position)) < 0.001,
    );
    const incoming = position.clone().sub(at(phase - epsilon));
    const outgoing = at(phase + epsilon).sub(position);
    assert.ok(incoming.length() > 0.0001 && outgoing.length() > 0.0001);
    assert.ok(
      Math.abs(incoming.length() / outgoing.length() - 1) < 0.02,
      'Speed must join smoothly at each shot',
    );
    assert.ok(incoming.normalize().dot(outgoing.normalize()) > 0.999);
  }
  assert.ok(at(0).distanceTo(at(1)) < 1e-8);
  let inside = 0,
    aerial = 0;
  const views = new Set();
  const speeds = { inside: [], aerial: [] };
  for (let index = 0; index <= 1000; index++) {
    const position = new THREE.Vector3();
    const pose = tour.sample(index / 1000, position, target);
    views.add(pose.view);
    if (Math.abs(position.x) < 55 && Math.abs(position.z) < 55 && position.y < 20) inside++;
    if (Math.hypot(position.x, position.z) > 120 && position.y > 70) aerial++;
    const speed = at(index / 1000 + epsilon).distanceTo(position) / epsilon;
    if (Math.abs(position.x) < 55 && Math.abs(position.z) < 55 && position.y < 20)
      speeds.inside.push(speed);
    if (Math.hypot(position.x, position.z) > 120 && position.y > 70) speeds.aerial.push(speed);
    assert.ok(position.y > 4);
    assert.ok(position.distanceTo(target) > 8);
    assert.ok(pose.fov >= 34 && pose.fov <= 64);
  }
  assert.ok(inside > 200, 'The flight must spend time between buildings, not just outside the map');
  assert.ok(aerial > 200, 'The flight must also cover the full campus from above');
  const average = (values) => values.reduce((sum, value) => sum + value, 0) / values.length;
  assert.ok(
    average(speeds.aerial) > average(speeds.inside) * 2,
    'Aerial transfers should be faster than architectural reveals',
  );
  assert.equal(views.size, journey.campusViews.length);
});

const framingCode = ts.transpileModule(
  readFileSync(new URL('../src/features/landing/campusFraming.ts', import.meta.url), 'utf8'),
  {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  },
).outputText;
const framing = {};
new Function('require', 'exports', framingCode)((name) => {
  if (name === 'three') return THREE;
  throw new Error(`Unexpected module: ${name}`);
}, framing);

async function loadModel() {
  const bytes = readFileSync(new URL('../public/models/uit-campus.glb', import.meta.url));
  const { scene } = await new GLTFLoader().parseAsync(
    bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    '',
  );
  scene.updateMatrixWorld(true);
  return scene;
}

test('expanded overview canvas preserves camera distance and pixel scale without cutting the map', async () => {
  const points = framing.campusFramePoints(await loadModel());
  const views = journey.campusViews.map((view) => ({
    position: new THREE.Vector3(...view.position),
    target: new THREE.Vector3(...view.target),
  }));
  for (let angle = 0; angle < Math.PI * 2; angle += Math.PI / 8)
    for (const elevation of [0.3, 0.6, 1.1])
      views.push({
        position: new THREE.Vector3(
          170 * Math.cos(angle),
          170 * Math.tan(elevation),
          170 * Math.sin(angle),
        ),
        target: new THREE.Vector3(0, 6, 0),
      });
  const projected = new THREE.Vector3();
  for (const [width, height, availableWidth] of [
    [380, 390, 380],
    [534, 500, 1014],
    [771, 560, 1430],
    [771, 620, 1830],
  ]) {
    for (const view of views) {
      const camera = new THREE.PerspectiveCamera(
        framing.CAMPUS_OVERVIEW_FOV,
        width / height,
        0.25,
        700,
      );
      camera.position.copy(view.position);
      camera.lookAt(view.target);
      camera.updateMatrixWorld();
      const size = framing.frameCampusOverview(camera, points, width, height, availableWidth);
      assert.ok(camera.position.equals(view.position), 'Framing must never back the camera away');
      assert.ok(size.width <= availableWidth + 1);
      if (availableWidth > 1000) {
        const referenceFov = THREE.MathUtils.radToDeg(
          2 *
            Math.atan(
              Math.tan(THREE.MathUtils.degToRad(34) / 2) * Math.max(1, (1.3 * height) / width),
            ),
        );
        assert.ok(
          Math.abs(camera.fov - referenceFov) < 1e-6,
          'Desktop framing must preserve the original magnification',
        );
      }
      for (const point of points) {
        projected.copy(point).project(camera);
        assert.ok(
          Math.abs(projected.x) < 0.963 && Math.abs(projected.y) < 0.963,
          `Clipped campus at width ${width}: ${projected.toArray()}`,
        );
      }
    }
  }
});

test('the cinematic rail clears the actual roofs, walls and trees while flying inside the campus', async () => {
  const scene = await loadModel();
  const tour = tourModule.createCampusTour();
  const position = new THREE.Vector3(),
    target = new THREE.Vector3();
  const ray = new THREE.Raycaster(new THREE.Vector3(), new THREE.Vector3(0, -1, 0));
  for (let index = 0; index <= 2100; index++) {
    tour.sample(index / 2100, position, target);
    if (Math.abs(position.x) > 65 || Math.abs(position.z) > 65) continue;
    ray.ray.origin.set(position.x, 100, position.z);
    const hit = ray.intersectObject(scene, true)[0];
    assert.ok(
      !hit || hit.point.y < position.y - 0.7,
      `Flight intersects ${hit?.object.parent.name} at ${position.toArray()}`,
    );
  }
});

test('visitor path and gait are deterministic across viewers, finite and inside the campus', () => {
  for (let index = 0; index < 120; index++) {
    const seed = journey.visitorSeed(`visitor-${index}`);
    assert.equal(seed, journey.visitorSeed(`visitor-${index}`));
    const time = 1794900000 + index / 10;
    const pose = journey.walkingPose(seed, time);
    assert.deepEqual(pose, journey.walkingPose(seed, time));
    Object.values(pose).forEach((value) => assert.ok(Number.isFinite(value)));
    assert.ok(Math.abs(pose.x) < 59 && Math.abs(pose.z) < 57);
    const next = journey.walkingPose(seed, time + 0.01);
    assert.ok(Math.hypot(next.x - pose.x, next.z - pose.z) <= 0.02);
  }
});

test('walking routes clear the actual GLB buildings, gates and trees at ground level', async () => {
  const bytes = readFileSync(new URL('../public/models/uit-campus.glb', import.meta.url));
  const { scene } = await new GLTFLoader().parseAsync(
    bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    '',
  );
  scene.updateMatrixWorld(true);
  const ray = new THREE.Raycaster(new THREE.Vector3(), new THREE.Vector3(0, -1, 0));
  for (const path of journey.walkingPaths) {
    for (let index = 0; index < path.length; index++) {
      const start = path[index],
        end = path[(index + 1) % path.length];
      const steps = Math.ceil(Math.hypot(end[0] - start[0], end[1] - start[1]));
      for (let step = 0; step <= steps; step++) {
        const t = step / steps;
        const x = start[0] + (end[0] - start[0]) * t;
        const z = start[1] + (end[1] - start[1]) * t;
        ray.ray.origin.set(x, 100, z);
        const hits = ray.intersectObject(scene, true);
        assert.ok(hits.length > 0, `No campus ground at ${x}, ${z}`);
        assert.ok(
          hits[0].point.y < 0.5,
          `Route intersects ${hits[0].object.parent.name} at ${x}, ${z}`,
        );
      }
    }
  }
});
