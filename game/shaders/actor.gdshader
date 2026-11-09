// Characters (UMBRA's vertex-coloured models): Lambert-ish albedo from the vertex colours, a rim of light so the
// silhouette reads against the dark crypt, a hit flash and a slow darkening after death. One pass, no overlay.
shader_type spatial;

uniform vec4 rim_color : source_color = vec4(1.0, 0.78, 0.55, 1.0);
uniform float rim_strength = 0.55;
uniform vec4 flash_color : source_color = vec4(1.0, 0.35, 0.25, 1.0);
uniform float flash = 0.0;
uniform float dead = 0.0;
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);   // per enemy type until the art phase (white = the model as is)

void fragment() {
	vec3 base = COLOR.rgb;
	ALBEDO = base * tint.rgb * mix(1.0, 0.35, dead);
	ROUGHNESS = 0.72;
	METALLIC = 0.05;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float rim = pow(1.0 - facing, 3.0) * rim_strength * (1.0 - dead);
	EMISSION = rim_color.rgb * rim + flash_color.rgb * flash;
}
