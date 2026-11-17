import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import {
  ArrowDown,
  ArrowUpRight,
  Check,
  ChevronRight,
  GraduationCap,
  Move,
  Pause,
  Play,
  RotateCcw,
} from 'lucide-react';
import { useAuth } from '@/shared/auth';
import { ThesisSearch } from '../library/ThesisSearch';
import { campusViews } from './campusJourney';
import { CampusLoading } from './CampusLoading';
import { useCampusPresence } from './campusPresenceContext';

const CampusScene = lazy(() => import('./CampusScene'));

export function HomeEntry() {
  const { isAuthenticated, isLoading } = useAuth();
  return isAuthenticated && !isLoading ? <Navigate to="/dashboard" replace /> : <LandingPage />;
}

export function LandingPage() {
  const [view, setView] = useState(0);
  const [cameraReset, setCameraReset] = useState(0);
  const [interactive, setInteractive] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [walking, setWalking] = useState(true);
  const presence = useCampusPresence();
  const { isAuthenticated } = useAuth();
  useEffect(() => {
    document.title = 'UGTM · Từ ý tưởng đến dấu ấn khóa luận';
  }, []);
  useEffect(() => {
    if (!playing) return;
    const stop = () => {
      if (document.hidden) setPlaying(false);
    };
    document.addEventListener('visibilitychange', stop);
    return () => {
      document.removeEventListener('visibilitychange', stop);
    };
  }, [playing]);
  return (
    <>
      <section className="landing-hero" aria-labelledby="landing-title">
        <div className="public-container landing-hero-inner">
          <div className="landing-hero-copy">
            <h1 id="landing-title">
              Từ ý tưởng tại{' '}
              <span className="hero-uit">
                UIT<span className="hero-period">.</span>
              </span>
              <br />
              Đến dấu ấn
              <br />
              <span className="hero-blue">khóa luận.</span>
            </h1>
            <p>
              Khám phá những công trình đã hoàn thành.
              <br className="desktop-break" /> Viết tiếp hành trình nghiên cứu của riêng bạn.
            </p>
            <div className="landing-hero-actions">
              <a href="#library" className="public-button public-button-blue">
                Tra cứu khóa luận
                <ArrowUpRight size={19} />
              </a>
              <a href="#campus" className="public-button public-button-outline">
                Khám phá UIT
                <ArrowDown size={17} />
              </a>
            </div>
            <div className="hero-product-note">
              <GraduationCap size={18} />
              <span>UGTM · Đồng hành cùng khóa luận tốt nghiệp</span>
            </div>
          </div>
          <div
            id="campus"
            className="hero-campus-explorer"
            role="region"
            aria-label="Khám phá khuôn viên UIT 3D"
            tabIndex={-1}
          >
            <div className="hero-campus">
              <div className="hero-campus-disc" />
              <Suspense fallback={<CampusLoading />}>
                <CampusScene
                  view={view}
                  resetKey={cameraReset}
                  interactive={interactive}
                  visitors={presence.snapshot?.visitors}
                  serverTime={presence.snapshot?.serverTime}
                  walking={walking}
                  touring={playing}
                  onViewChange={setView}
                />
              </Suspense>
            </div>
            <div className="hero-campus-panel">
              <div className="campus-explorer-status">
                <span className="campus-presence" role="status">
                  <span
                    className={`campus-caption-dot ${presence.status !== 'connected' ? 'is-offline' : ''}`}
                  />
                  {presence.status === 'connected'
                    ? `${presence.snapshot?.visitors.length ?? 0} người đang online`
                    : presence.status === 'error'
                      ? 'Chưa thể đồng bộ người online'
                      : 'Đang kết nối khuôn viên…'}
                </span>
                <span className="campus-current-view" aria-live="polite">
                  {playing ? `Tham quan · ${campusViews[view].name}` : campusViews[view].name}
                </span>
              </div>
              <div className="campus-explorer-controls">
                <button
                  type="button"
                  className={`campus-control ${interactive ? 'active' : ''}`}
                  aria-pressed={interactive}
                  onClick={() => {
                    setInteractive(!interactive);
                    setPlaying(false);
                  }}
                >
                  <Move size={16} />
                  {interactive ? 'Đang xoay tự do' : 'Tự xoay mô hình'}
                </button>
                <button
                  type="button"
                  className="campus-control campus-play"
                  aria-pressed={playing}
                  onClick={() => {
                    setPlaying(!playing);
                    setInteractive(false);
                  }}
                >
                  {playing ? <Pause size={15} /> : <Play size={15} />}
                  {playing ? 'Dừng tham quan' : 'Tham quan tự động'}
                </button>
                {interactive && (
                  <button
                    type="button"
                    className="campus-control"
                    aria-label="Đặt lại góc nhìn"
                    onClick={() => {
                      setInteractive(false);
                      setView(0);
                      setCameraReset((current) => current + 1);
                    }}
                  >
                    <RotateCcw size={16} />
                  </button>
                )}
              </div>
              <div className="campus-explorer-views">
                <div
                  className="campus-view-buttons"
                  role="group"
                  aria-label="Chọn góc nhìn khuôn viên"
                >
                  {campusViews.map((item, index) => (
                    <button
                      type="button"
                      key={item.name}
                      aria-pressed={view === index}
                      onClick={() => {
                        setView(index);
                        setCameraReset((current) => current + 1);
                        setPlaying(false);
                        setInteractive(false);
                      }}
                    >
                      {item.name}
                      {view === index && <span />}
                    </button>
                  ))}
                </div>
              </div>
              <div className="campus-explorer-footer">
                <button
                  type="button"
                  className="campus-motion-toggle"
                  aria-pressed={!walking}
                  onClick={() => setWalking(!walking)}
                >
                  {walking ? 'Dừng người đi bộ' : 'Cho người đi bộ'}
                </button>
                <a
                  href="https://tuyensinh.uit.edu.vn/uit-tour-360/"
                  target="_blank"
                  rel="noreferrer"
                >
                  UIT thực tế 360° <ArrowUpRight size={14} />
                </a>
              </div>
            </div>
          </div>
        </div>
        <div className="public-container hero-bottom-line">
          <span>Trường Đại học Công nghệ Thông tin · ĐHQG-HCM</span>
          <a href="#library">
            Khám phá kho khóa luận
            <ArrowDown size={15} />
          </a>
        </div>
      </section>

      <section id="library" className="landing-library-section" aria-labelledby="library-title">
        <div className="public-container">
          <div className="public-section-heading">
            <h2 id="library-title">
              Những ý tưởng đã
              <br />
              <span>đi xa hơn giảng đường.</span>
            </h2>
            <p>
              Tra cứu khóa luận qua các năm học.
              <br />
              Tìm cảm hứng cho bước tiếp theo của bạn.
            </p>
          </div>
          <ThesisSearch preview />
        </div>
      </section>

      <section id="about" className="landing-about-section" aria-labelledby="about-title">
        <div className="public-container landing-about-grid">
          <div>
            <h2 id="about-title">
              Một hành trình.
              <br />
              <span>Mọi cột mốc.</span>
            </h2>
            <p>
              UGTM kết nối các bước thực hiện khóa luận, giúp sinh viên, giảng viên và Khoa theo dõi
              công việc trong cùng một hệ thống.
            </p>
            <Link to={isAuthenticated ? '/dashboard' : '/login'} className="public-text-link">
              {isAuthenticated ? 'Vào hệ thống UGTM' : 'Đăng nhập để bắt đầu'}
              <ArrowUpRight size={19} />
            </Link>
          </div>
          <ol className="landing-workflow">
            {[
              [
                'Tìm hướng nghiên cứu',
                'Khám phá đề tài, đăng ký nhóm và xác định hướng đi cùng giảng viên.',
              ],
              [
                'Phát triển ý tưởng',
                'Nộp đề cương, theo dõi các mốc thực hiện và tiếp nhận phản hồi.',
              ],
              ['Bảo vệ công trình', 'Hoàn thiện báo cáo, tham gia bảo vệ và theo dõi kết quả.'],
              [
                'Lưu giữ tri thức',
                'Nộp hồ sơ lưu chiểu, hoàn tất hành trình và tiếp nối những ý tưởng mới.',
              ],
            ].map(([title, description], index) => (
              <li key={title}>
                <span className="workflow-step">{index + 1}</span>
                <div>
                  <h3>{title}</h3>
                  <p>{description}</p>
                </div>
                <Check size={17} aria-hidden="true" />
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="landing-entry-section">
        <div className="public-container">
          <div>
            <h2>
              Ý tưởng tiếp theo,
              <br />
              <span>bắt đầu từ bạn.</span>
            </h2>
            <p>
              Từ một câu hỏi nhỏ đến một công trình của riêng mình.
              <br />
              UGTM đồng hành cùng bạn qua từng cột mốc.
            </p>
          </div>
          <Link
            to={isAuthenticated ? '/dashboard' : '/login'}
            className="public-button public-button-orange"
          >
            {isAuthenticated ? 'Vào hệ thống UGTM' : 'Đăng nhập UGTM'}
            <ChevronRight size={20} />
          </Link>
        </div>
        <ArrowUpRight className="entry-watermark" aria-hidden="true" />
      </section>
    </>
  );
}
