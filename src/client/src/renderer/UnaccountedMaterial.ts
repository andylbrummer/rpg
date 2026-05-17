import * as THREE from 'three';

const vertexShader = `
  uniform float uTime;
  varying vec2 vUv;
  varying vec3 vViewPosition;
  varying vec3 vNormal;

  void main() {
    vUv = uv;
    vNormal = normalize(normalMatrix * normal);

    // Subtle vertex displacement — twitch/float effect
    vec3 pos = position;
    float twitch = sin(uTime * 8.0 + position.x * 10.0) * 0.02;
    float floatY = sin(uTime * 2.0 + position.z * 5.0) * 0.03;
    pos.x += twitch;
    pos.y += floatY;

    vec4 mvPosition = modelViewMatrix * vec4(pos, 1.0);
    vViewPosition = -mvPosition.xyz;
    gl_Position = projectionMatrix * mvPosition;
  }
`;

const fragmentShader = `
  uniform float uTime;
  uniform vec3 uColor;
  uniform vec3 uEmissive;
  varying vec2 vUv;
  varying vec3 vViewPosition;
  varying vec3 vNormal;

  void main() {
    vec3 viewDir = normalize(vViewPosition);
    float fresnel = 1.0 - abs(dot(viewDir, vNormal));

    // Chromatic aberration: offset RGB channels by view angle
    float aberration = fresnel * 0.04;
    vec2 offsetR = vec2(aberration, 0.0);
    vec2 offsetB = vec2(-aberration, 0.0);

    // Simple procedural noise for glitch texture
    float noise = fract(sin(dot(vUv * 50.0 + uTime, vec2(12.9898, 78.233))) * 43758.5453);
    float glitch = step(0.97, noise) * 0.3;

    vec3 baseColor = uColor;
    // Inverted brightness at edges
    baseColor = mix(baseColor, 1.0 - baseColor, fresnel * 0.3);

    // Emissive glow pulsing
    vec3 emissive = uEmissive * (0.7 + 0.3 * sin(uTime * 3.0));

    vec3 finalColor = baseColor + emissive * fresnel + glitch;
    gl_FragColor = vec4(finalColor, 1.0);
  }
`;

export function createUnaccountedMaterial(): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uColor: { value: new THREE.Color(0xeeeeee) },
      uEmissive: { value: new THREE.Color(0x8800ff) },
    },
    vertexShader,
    fragmentShader,
    side: THREE.DoubleSide,
  });
}
