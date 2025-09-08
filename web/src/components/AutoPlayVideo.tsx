import { useEffect, useRef } from "react";

interface AutoPlayVideoProps {
  src: string;
  type: string;
  className?: string;
}

export default function AutoPlayVideo({ src, type, className }: AutoPlayVideoProps) {
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          video.play().catch(() => {});
        } else {
          video.pause();
        }
      },
      { threshold: 0.5 }
    );

    observer.observe(video);
    return () => observer.disconnect();
  }, []);

  return (
    <video
      ref={videoRef}
      className={`mx-auto w-full rounded-2xl border shadow-lg ${className ?? ""}`}
      loop
      muted
      playsInline
      preload="metadata"
    >
      <source src={src} type={type} />
    </video>
  );
}
