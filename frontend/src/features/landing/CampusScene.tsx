import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import campusPhoto from '../../../assets/background_campus.png';
import { campusViews, TOUR_DURATION_MS } from './campusJourney';
import { createCampusWalkers } from './campusWalkers';
import { createCampusTour } from './campusTour';
import { CampusLoading } from './CampusLoading';
import { CAMPUS_OVERVIEW_FOV, campusFramePoints, frameCampusOverview } from './campusFraming';
let campusAsset: Promise<THREE.Group> | undefined;
function loadCampus() {
  campusAsset ??= new GLTFLoader()
    .loadAsync('/models/uit-campus.glb')
    .then((gltf) => gltf.scene)
    .catch((error) => {
      campusAsset = undefined;
      throw error;
    });
  return campusAsset;
}

export default function CampusScene({
  view = 0,
  resetKey = 0,
  interactive = false,
  zoomStep = 0,
  visitors = [],
  serverTime,
  walking = true,
  touring = false,
  onViewChange,
  onReady,
}: {
  view?: number;
  resetKey?: number;
  interactive?: boolean;
  zoomStep?: number;
  visitors?: string[];
  serverTime?: number;
  walking?: boolean;
  touring?: boolean;
  onViewChange?: (index: number) => void;
  onReady?: () => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  const controller = useRef<{
    select: (index: number, key: number) => void;
    tour: (enabled: boolean) => void;
    zoom: (steps: number) => void;
    controls: OrbitControls;
    presence: (ids: string[], time?: number) => void;
    motion: (enabled: boolean) => void;
  } | null>(null);
  const appliedZoomStep = useRef(0);
  const readyCallback = useRef(onReady);
  const viewCallback = useRef(onViewChange);
  useEffect(() => {
    readyCallback.current = onReady;
    viewCallback.current = onViewChange;
  }, [onReady, onViewChange]);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');

  useEffect(() => {
    const container = host.current;
    if (!container) return;
    const element: HTMLDivElement = container;
    let disposed = false,
      visible = false,
      frame = 0;
    let renderer: THREE.WebGLRenderer | undefined;
    let controls: OrbitControls | undefined;
    let observer: ResizeObserver | undefined;
    let targetPosition: THREE.Vector3 | null = new THREE.Vector3(...campusViews[0].position);
    const targetLook = new THREE.Vector3(...campusViews[0].target);
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(CAMPUS_OVERVIEW_FOV, 1, 0.25, 700);
    camera.position.copy(targetPosition);
    const fill = new THREE.HemisphereLight('#eef7ff', '#92977f', 3.1);
    const sun = new THREE.DirectionalLight('#fff3dd', 3.8);
    sun.position.set(-60, 100, 65);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -100;
    sun.shadow.camera.right = 100;
    sun.shadow.camera.top = 100;
    sun.shadow.camera.bottom = -100;
    sun.shadow.camera.far = 260;
    sun.shadow.normalBias = 0.1;
    scene.add(fill, sun);
    const shadow = new THREE.Mesh(
      new THREE.PlaneGeometry(600, 600),
      new THREE.ShadowMaterial({ opacity: 0.12 }),
    );
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = -3.5;
    shadow.receiveShadow = true;
    scene.add(shadow);
    const walkers = createCampusWalkers(scene);
    const tour = createCampusTour();
    let tourRunning = false;
    let tourPhase = 0;
    let tourChapter = 0;
    let selectedView = -1;
    let selectedKey = -1;
    let blendElapsed = 1;
    let entryFov = CAMPUS_OVERVIEW_FOV;
    let entryOffsetX = 0,
      entryOffsetY = 0;
    let framing: THREE.Vector3[] = [];
    let overview = true;
    let overviewNeedsFit = true;
    let viewportWidth = 1,
      viewportHeight = 1;
    let reducedTourTimer: ReturnType<typeof setTimeout> | undefined;
    const tourPosition = new THREE.Vector3();
    const tourTarget = new THREE.Vector3();
    const entryPosition = new THREE.Vector3();
    const entryTarget = new THREE.Vector3();
    let walkingEnabled = true;
    let timeOffset = 0;
    let frozenTime = Date.now() / 1000;
    const clock = new THREE.Timer();
    clock.connect(document);
    function setRenderSize(width: number, height: number) {
      if (
        renderer &&
        (renderer.domElement.style.width !== `${width}px` ||
          renderer.domElement.style.height !== `${height}px`)
      )
        renderer.setSize(width, height);
    }
    function resetViewport() {
      camera.clearViewOffset();
      camera.aspect = viewportWidth / viewportHeight;
      camera.updateProjectionMatrix();
      setRenderSize(viewportWidth, viewportHeight);
      if (renderer) {
        renderer.domElement.style.left = '0px';
        renderer.domElement.style.top = '0px';
      }
    }
    function beginManualView() {
      targetPosition = null;
      if (!overview || !renderer) return;
      // Convert the expanded overview into the fixed interaction viewport without
      // moving a single pixel. Auto-fitting after a dolly would cancel the zoom.
      const offsetX =
        (camera.view?.enabled ? camera.view.offsetX : 0) -
        (parseFloat(renderer.domElement.style.left) || 0);
      const offsetY =
        (camera.view?.enabled ? camera.view.offsetY : 0) -
        (parseFloat(renderer.domElement.style.top) || 0);
      resetViewport();
      camera.setViewOffset(
        viewportWidth,
        viewportHeight,
        offsetX,
        offsetY,
        viewportWidth,
        viewportHeight,
      );
      overview = false;
    }
    function render() {
      frame = 0;
      if (disposed || !visible || document.hidden || !renderer || !controls) {
        clearTimeout(reducedTourTimer);
        reducedTourTimer = undefined;
        return;
      }
      if (!tourRunning || !reduced.matches) {
        clearTimeout(reducedTourTimer);
        reducedTourTimer = undefined;
      }
      clock.update();
      const delta = clock.getDelta();
      const dt = Math.min(delta, 0.05);
      const amount = reduced.matches ? 1 : 1 - Math.exp(-5 * dt);
      if (tourRunning) {
        if (!reduced.matches) tourPhase = (tourPhase + (delta * 1000) / TOUR_DURATION_MS) % 1;
        const pose = tour.sample(tourPhase, tourPosition, tourTarget);
        if (reduced.matches) {
          tourPosition.fromArray(campusViews[pose.view].position);
          tourTarget.fromArray(campusViews[pose.view].target);
        }
        blendElapsed = Math.min(1, blendElapsed + dt / 0.65);
        const blend = reduced.matches
          ? 1
          : blendElapsed ** 3 * (blendElapsed * (blendElapsed * 6 - 15) + 10);
        camera.position.copy(entryPosition).lerp(tourPosition, blend);
        controls.target.copy(entryTarget).lerp(tourTarget, blend);
        camera.fov = THREE.MathUtils.lerp(entryFov, pose.fov, blend);
        if (blend < 1)
          camera.setViewOffset(
            viewportWidth,
            viewportHeight,
            entryOffsetX * (1 - blend),
            entryOffsetY * (1 - blend),
            viewportWidth,
            viewportHeight,
          );
        else camera.clearViewOffset();
        camera.updateProjectionMatrix();
        camera.lookAt(controls.target);
        if (pose.view !== tourChapter) {
          tourChapter = pose.view;
          viewCallback.current?.(pose.view);
        }
      } else if (targetPosition) {
        camera.position.lerp(targetPosition, amount);
        controls.target.lerp(targetLook, amount);
        if (camera.position.distanceTo(targetPosition) < 0.03) targetPosition = null;
      }
      // OrbitControls must not clamp a cinematic camera that looks upward at E.
      const changed = tourRunning ? false : controls.update();
      if (
        (overview && !tourRunning && (changed || overviewNeedsFit || targetPosition)) ||
        (tourRunning && reduced.matches)
      ) {
        const size = frameCampusOverview(
          camera,
          framing,
          viewportWidth,
          viewportHeight,
          innerWidth - 10,
        );
        setRenderSize(size.width, size.height);
        const bounds = element.getBoundingClientRect();
        const left = THREE.MathUtils.clamp(
          bounds.left + bounds.width / 2 - size.width / 2,
          5,
          innerWidth - size.width - 5,
        );
        renderer.domElement.style.left = `${left - bounds.left}px`;
        renderer.domElement.style.top = `${viewportHeight - size.height}px`;
        overviewNeedsFit = false;
      }
      const animateWalkers = walkingEnabled && !reduced.matches;
      walkers.update(
        animateWalkers ? (Date.now() + timeOffset) / 1000 : frozenTime,
        animateWalkers,
      );
      renderer.render(scene, camera);
      // Static when reduced motion is requested; controls and resizes request a fresh frame.
      if (
        (changed && !reduced.matches) ||
        targetPosition ||
        (walkers.count > 0 && animateWalkers) ||
        (tourRunning && !reduced.matches)
      )
        requestRender();
      else if (tourRunning && reduced.matches && !reducedTourTimer)
        reducedTourTimer = setTimeout(() => {
          reducedTourTimer = undefined;
          tourPhase = tour.phaseForView((tourChapter + 1) % campusViews.length);
          requestRender();
        }, TOUR_DURATION_MS / campusViews.length);
    }
    function requestRender() {
      if (!frame) frame = requestAnimationFrame(render);
    }
    async function initialize() {
      try {
        renderer = new THREE.WebGLRenderer({
          antialias: true,
          alpha: true,
          powerPreference: 'low-power',
        });
        renderer.setPixelRatio(Math.min(devicePixelRatio, innerWidth < 700 ? 1.25 : 1.7));
        renderer.setClearColor(0x000000, 0);
        renderer.outputColorSpace = THREE.SRGBColorSpace;
        renderer.toneMapping = THREE.ACESFilmicToneMapping;
        renderer.toneMappingExposure = 1.15;
        renderer.shadowMap.enabled = true;
        renderer.shadowMap.type = THREE.PCFShadowMap;
        renderer.domElement.setAttribute('aria-hidden', 'true');
        renderer.domElement.addEventListener('webglcontextlost', contextLost);
        element.appendChild(renderer.domElement);
        controls = new OrbitControls(camera, renderer.domElement);
        controls.target.copy(targetLook);
        controls.enableZoom = true;
        controls.zoomSpeed = 0.8;
        controls.minDistance = 35;
        controls.maxDistance = 450;
        controls.enablePan = false;
        controls.enableDamping = !reduced.matches;
        controls.dampingFactor = 0.08;
        controls.minPolarAngle = 0.08;
        controls.maxPolarAngle = 1.35;
        controls.enabled = false;
        controls.addEventListener('start', beginManualView);
        controls.addEventListener('change', requestRender);
        controller.current = {
          controls,
          zoom(steps) {
            beginManualView();
            const distance = camera.position.distanceTo(controls!.target);
            const next = THREE.MathUtils.clamp(
              distance * 0.8 ** steps,
              controls!.minDistance,
              controls!.maxDistance,
            );
            camera.position
              .sub(controls!.target)
              .multiplyScalar(next / distance)
              .add(controls!.target);
            controls!.update();
            requestRender();
          },
          tour(enabled) {
            if (enabled === tourRunning) return;
            tourRunning = enabled;
            targetPosition = null;
            controls!.enableDamping = !enabled && !reduced.matches;
            if (enabled) {
              overview = false;
              controls!.maxPolarAngle = Math.PI - 0.08;
              entryPosition.copy(camera.position);
              entryTarget.copy(controls!.target);
              entryFov = camera.fov;
              entryOffsetX =
                (camera.view?.enabled ? camera.view.offsetX : 0) -
                (parseFloat(renderer!.domElement.style.left) || 0);
              entryOffsetY = camera.view?.enabled
                ? camera.view.offsetY + camera.view.height - viewportHeight
                : 0;
              resetViewport();
              blendElapsed = 0;
              clock.reset();
            }
            requestRender();
          },
          presence(ids, time) {
            walkers.setVisitors(ids);
            if (time !== undefined) timeOffset = time - Date.now();
            requestRender();
          },
          motion(enabled) {
            walkingEnabled = enabled;
            frozenTime = (Date.now() + timeOffset) / 1000;
            requestRender();
          },
          select(index, key) {
            // Chapter labels follow the running curve; only a user selection interrupts it.
            if (tourRunning && key === selectedKey) return;
            if (selectedView === index && selectedKey === key) return;
            selectedView = index;
            selectedKey = key;
            tourRunning = false;
            overview = true;
            overviewNeedsFit = true;
            controls!.enableDamping = !reduced.matches;
            controls!.maxPolarAngle = 1.35;
            tourPhase = tour.phaseForView(index);
            tourChapter = index;
            const selected = campusViews[index] ?? campusViews[0];
            targetPosition = new THREE.Vector3(...selected.position);
            targetLook.set(selected.target[0], selected.target[1], selected.target[2]);
            requestRender();
          },
        };
        observer = new ResizeObserver(() => {
          if (!renderer) return;
          const { width, height } = element.getBoundingClientRect();
          if (!width || !height) return;
          viewportWidth = width;
          viewportHeight = height;
          resetViewport();
          overviewNeedsFit = true;
          if (overview && !controls?.enabled) {
            const selected = campusViews[selectedView] ?? campusViews[0];
            targetPosition = new THREE.Vector3(...selected.position);
          }
          requestRender();
        });
        observer.observe(element);
        const asset = await loadCampus();
        if (disposed) return;
        const model = asset.clone(true);
        model.traverse((object) => {
          if (object instanceof THREE.Mesh) {
            object.castShadow = true;
            object.receiveShadow = true;
          }
        });
        scene.add(model);
        framing = campusFramePoints(model);
        overviewNeedsFit = true;
        setStatus('ready');
        readyCallback.current?.();
        requestRender();
      } catch {
        if (!disposed) setStatus('error');
      }
    }
    function contextLost(event: Event) {
      event.preventDefault();
      visible = false;
      setStatus('error');
      cancelAnimationFrame(frame);
    }
    let initialized = false;
    const intersection = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !visible) clock.reset();
        visible = entry.isIntersecting;
        if (visible && !initialized) {
          initialized = true;
          void initialize();
        }
        requestRender();
      },
      { rootMargin: '150px' },
    );
    intersection.observe(element);
    document.addEventListener('visibilitychange', requestRender);
    function syncMotion() {
      if (controls) controls.enableDamping = !reduced.matches && !tourRunning;
      if (tourRunning) resetViewport();
      frozenTime = (Date.now() + timeOffset) / 1000;
      requestRender();
    }
    reduced.addEventListener('change', syncMotion);
    return () => {
      disposed = true;
      clock.dispose();
      cancelAnimationFrame(frame);
      clearTimeout(reducedTourTimer);
      intersection.disconnect();
      observer?.disconnect();
      document.removeEventListener('visibilitychange', requestRender);
      reduced.removeEventListener('change', syncMotion);
      controls?.dispose();
      controller.current = null;
      shadow.geometry.dispose();
      walkers.dispose();
      sun.shadow.dispose();
      (shadow.material as THREE.Material).dispose();
      renderer?.domElement.removeEventListener('webglcontextlost', contextLost);
      renderer?.dispose();
      renderer?.domElement.remove();
      // GLB geometries/materials remain cached for subsequent route visits.
    };
  }, []);

  useEffect(() => {
    controller.current?.select(view, resetKey);
  }, [view, resetKey, status]);
  useEffect(() => {
    controller.current?.tour(touring);
  }, [touring, status]);
  useEffect(() => {
    if (controller.current) controller.current.controls.enabled = interactive;
  }, [interactive, status]);
  useEffect(() => {
    if (!controller.current) return;
    const steps = zoomStep - appliedZoomStep.current;
    if (steps) controller.current.zoom(steps);
    appliedZoomStep.current = zoomStep;
  }, [zoomStep, status]);
  useEffect(() => {
    controller.current?.presence(visitors, serverTime);
  }, [visitors, serverTime, status]);
  useEffect(() => {
    controller.current?.motion(walking);
  }, [walking, status]);

  return (
    <div
      ref={host}
      className={`campus-scene ${interactive ? 'is-interactive' : ''}`}
      role={status === 'ready' ? 'img' : undefined}
      aria-busy={status === 'loading'}
      aria-label={
        status === 'ready'
          ? `Mô hình tham chiếu khuôn viên UIT: ${campusViews[view]?.name ?? 'Cổng A'}`
          : undefined
      }
    >
      {status === 'error' && (
        <img
          className="campus-fallback"
          src={campusPhoto}
          alt="Tòa nhà E, Trường Đại học Công nghệ Thông tin"
        />
      )}
      {status === 'error' && (
        <span className="campus-status">Ảnh khuôn viên · 3D chưa khả dụng trên thiết bị này</span>
      )}
      {status === 'loading' && <CampusLoading />}
    </div>
  );
}
