import { CatmullRomCurve3, Vector3 } from 'three';
import { campusFlight, campusFlightBeats, campusViews } from './campusJourney';

export function createCampusTour() {
  const camera = new CatmullRomCurve3(
    campusFlight.map((view) => new Vector3(...view.position)),
    true,
  );
  const look = new CatmullRomCurve3(
    campusFlight.map((view) => new Vector3(...view.target)),
    true,
  );
  const total = campusFlightBeats.reduce((sum, beat) => sum + beat, 0);
  const durations = campusFlightBeats.map((beat) => beat / total);
  const stops = [0];
  for (const duration of durations) stops.push(stops[stops.length - 1] + duration);
  camera.arcLengthDivisions = campusFlight.length * 600;
  const lengths = camera.getLengths();
  const routeLength = lengths[lengths.length - 1];
  const arcStops = stops.map((_, index) => lengths[index * 600] / routeLength);
  const distances = durations.map((_, index) => arcStops[index + 1] - arcStops[index]);
  const speeds = distances.map((distance, index) => distance / durations[index]);
  const velocities = durations.map((_, index) => {
    const previous = speeds[(index + durations.length - 1) % durations.length];
    // Harmonic mean gives a monotone cubic with matching speed on both sides.
    return (2 * previous * speeds[index]) / (previous + speeds[index]);
  });
  const phases = campusViews.map(
    (_, index) =>
      stops[
        Math.max(
          0,
          campusFlight.findIndex((point) => point.view === index),
        )
      ],
  );
  return {
    phaseForView(index: number) {
      return phases[index] ?? 0;
    },
    sample(phase: number, position: Vector3, target: Vector3) {
      const progress = ((phase % 1) + 1) % 1;
      let index = 0;
      while (index < campusFlight.length - 1 && progress >= stops[index + 1] - 1e-10) index++;
      const local = Math.max(0, Math.min(1, (progress - stops[index]) / durations[index]));
      const square = local * local,
        cube = square * local;
      const startSpeed = (velocities[index] * durations[index]) / distances[index];
      const endSpeed =
        (velocities[(index + 1) % velocities.length] * durations[index]) / distances[index];
      const travel =
        (cube - 2 * square + local) * startSpeed +
        (-2 * cube + 3 * square) +
        (cube - square) * endSpeed;
      const arcPhase = arcStops[index] + travel * distances[index];
      const routePhase = camera.getUtoTmapping(Math.max(0, Math.min(1, arcPhase)), 0);
      camera.getPoint(routePhase, position);
      look.getPoint(routePhase, target);
      const current = campusFlight[index],
        next = campusFlight[(index + 1) % campusFlight.length];
      const blend = (1 - Math.cos(travel * Math.PI)) / 2;
      return { view: current.view, fov: current.fov + (next.fov - current.fov) * blend };
    },
  };
}
