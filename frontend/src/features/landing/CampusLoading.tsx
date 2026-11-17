export function CampusLoading() {
  return (
    <div className="campus-loading" role="status" aria-label="Đang tải mô hình khuôn viên UIT">
      <div className="campus-skeleton-map" aria-hidden="true">
        <span className="campus-skeleton-block campus-skeleton-tower" />
        <span className="campus-skeleton-block campus-skeleton-hall" />
        <span className="campus-skeleton-block campus-skeleton-wing" />
        <span className="campus-skeleton-path" />
      </div>
      <span className="campus-loading-label">Đang chuẩn bị khuôn viên 3D…</span>
    </div>
  );
}
