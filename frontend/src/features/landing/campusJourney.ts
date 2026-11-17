// Begin outside gate A (west side), then circle the campus in one consistent direction.
export const campusViews = [
  { name: 'Cổng A', position: [-178, 104, 50], target: [0, -5, -12] },
  { name: 'Tòa nhà E', position: [-110, 85, 138], target: [-5, 10, 8] },
  { name: 'Sân trường', position: [-7, 72, 172], target: [5, 5, 12] },
  { name: 'Giảng đường C', position: [97, 95, 145], target: [10, 6, 10] },
  { name: 'Khu dịch vụ', position: [164, 130, 61], target: [12, 5, 3] },
  { name: 'Sân nhà B', position: [173, 110, -49], target: [12, 7, -6] },
  { name: 'Toàn cảnh phía Đông', position: [113, 85, -143], target: [5, 6, -8] },
  { name: 'Phía sau khuôn viên', position: [7, 75, -172], target: [0, 6, -10] },
  { name: 'Nhà A · Thư viện', position: [-99, 95, -149], target: [-10, 7, -7] },
  { name: 'Toàn cảnh từ trên cao', position: [-169, 155, -63], target: [0, 3, 0] },
] as const;

export const TOUR_DURATION_MS = 20000;

// Adapt the reference preview's street-level journey to this GLB's real corridors,
// then climb over E and complete an aerial sweep around the whole campus.
export const campusFlight = [
  { position: [-178, 104, 50], target: [0, -5, -12], fov: 34, view: 0 },
  { position: [-85, 36, 24], target: [-36, 3, 18], fov: 48, view: 0 },
  { position: [-54, 10, 20], target: [-24, 6, 16], fov: 64, view: 0 },
  { position: [-31, 14, 16.8], target: [-5, 12, 16.5], fov: 64, view: 1 },
  { position: [-5, 14, 16.8], target: [12, 9, 8], fov: 64, view: 1 },
  { position: [11, 8, 5], target: [29, 7, -7], fov: 64, view: 2 },
  { position: [24, 7, 7], target: [30, 9, -10], fov: 64, view: 5 },
  { position: [28, 12, -5], target: [35, 18, -18], fov: 64, view: 5 },
  { position: [40, 14, -3], target: [26, 18, -15], fov: 60, view: 5 },
  { position: [49, 15, 13], target: [29, 6, 21], fov: 60, view: 4 },
  { position: [39, 18, 44], target: [17, 6, 29], fov: 60, view: 3 },
  { position: [12, 7, 51], target: [13, 5, 28], fov: 62, view: 2 },
  { position: [-22, 12, 46], target: [-8, 16, 29], fov: 60, view: 1 },
  { position: [-31, 43, 30], target: [-8, 29, 29], fov: 54, view: 1 },
  { position: [-4, 58, 25], target: [12, 5, 4], fov: 50, view: 1 },
  { position: [140, 94, 120], target: [15, 6, 12], fov: 38, view: 4 },
  { position: [116, 86, -105], target: [16, 6, -8], fov: 38, view: 6 },
  { position: [7, 105, -172], target: [0, 6, -10], fov: 34, view: 7 },
  { position: [-100, 108, -145], target: [-12, 7, -8], fov: 34, view: 8 },
  { position: [-169, 155, -63], target: [0, 3, 0], fov: 34, view: 9 },
  { position: [-190, 133, 20], target: [0, 0, -12], fov: 34, view: 0 },
] as const;

// Relative shot durations: linger at architecture, accelerate between locations.
// The tour interpolates speed across these beats, including the loop seam.
export const campusFlightBeats = [
  0.7, 0.8, 1.25, 1.4, 1.1, 0.8, 1.3, 1.3, 0.75, 0.8, 1.2, 1.05, 1.35, 1.35, 1, 0.9, 0.65, 0.65,
  0.85, 0.65, 0.65,
] as const;

// World coordinates checked against the generated GLB's ground-level footprints.
// The courtyard loop clears C's east wall and passes between the service blocks;
// the front promenade stays beyond C's southernmost corner (z = 44.8).
export const walkingPaths = [
  [
    [28, -6],
    [39, 0],
    [44, 9],
    [51, 17],
    [44, 9],
    [39, 0],
  ],
  [
    [0, 49],
    [16, 49],
    [34, 49],
    [16, 49],
  ],
] as const;

export function visitorSeed(id: string) {
  let value = 2166136261;
  for (const character of id) value = Math.imul(value ^ character.charCodeAt(0), 16777619);
  return value >>> 0;
}

export function walkingPose(seed: number, timeSeconds: number) {
  const path = walkingPaths[seed % walkingPaths.length];
  const lengths = path.map((point, index) => {
    const next = path[(index + 1) % path.length];
    return Math.hypot(next[0] - point[0], next[1] - point[1]);
  });
  const total = lengths.reduce((sum, length) => sum + length, 0);
  const speed = 1.15 + (seed % 7) * 0.08;
  let distance = (timeSeconds * speed + ((seed % 1000) / 1000) * total) % total;
  for (let index = 0; index < path.length; index++) {
    if (distance > lengths[index]) {
      distance -= lengths[index];
      continue;
    }
    const start = path[index],
      end = path[(index + 1) % path.length];
    const t = lengths[index] ? distance / lengths[index] : 0;
    return {
      x: start[0] + (end[0] - start[0]) * t,
      z: start[1] + (end[1] - start[1]) * t,
      heading: Math.atan2(end[0] - start[0], end[1] - start[1]),
      stride: Math.sin(timeSeconds * speed * 6 + seed) * 0.55,
    };
  }
  return { x: path[0][0], z: path[0][1], heading: 0, stride: 0 };
}
