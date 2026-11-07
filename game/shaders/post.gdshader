// Screen grade for the crypt (Médio/Alto): slight desaturation, cool shadows and warm highlights (torchlight against
// cold stone), a vignette that frames the hero, optional fine grain. Compatibility renderer: one full-screen pass.
shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform float saturation = 0.86;
uniform vec3 shadow_tint = vec3(0.86, 0.92, 1.08);
uniform vec3 highlight_tint = vec3(1.08, 1.0, 0.88);
uniform float vignette = 0.42;
uniform float grain = 0.0;
uniform float contrast = 1.06;

void fragment() {
	vec3 c = texture(screen, SCREEN_UV).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, saturation);
	c *= mix(shadow_tint, highlight_tint, smoothstep(0.05, 0.55, l));
	c = (c - 0.5) * contrast + 0.5;
	vec2 d = SCREEN_UV - vec2(0.5, 0.46);
	c *= 1.0 - vignette * smoothstep(0.08, 0.55, dot(d, d) * 1.6);
	if (grain > 0.0) {
		float n = fract(sin(dot(FRAGCOORD.xy + vec2(TIME * 61.0, TIME * 17.0), vec2(12.9898, 78.233))) * 43758.5453);
		c += (n - 0.5) * grain;
	}
	COLOR = vec4(max(c, vec3(0.0)), 1.0);
}
