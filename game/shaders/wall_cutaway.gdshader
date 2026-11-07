// Crypt wall: ashlar blocks in running bond (rows 0.36 high, blocks 0.62 long, every other row shifted), per-block
// tint, dark mortar, grime from a noise texture, darker foot and dark caps. A see-through window dissolves
// fragments between the camera and the hero (ordered dither), for every camera yaw and the top-down view.
// Compatibility renderer (GLSL ES 3).
shader_type spatial;

uniform sampler2D grime : filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec4 block_dark : source_color = vec4(0.17, 0.155, 0.15, 1.0);
uniform vec4 block_light : source_color = vec4(0.38, 0.35, 0.32, 1.0);
uniform vec4 mortar : source_color = vec4(0.06, 0.055, 0.055, 1.0);
uniform float wall_height = 2.2;
uniform vec3 hero_position;
uniform vec3 camera_position;
uniform float cut_radius = 3.6;
uniform float cut_enabled = 1.0;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0));
	int y = int(mod(p.y, 4.0));
	int i = x + y * 4;
	float m[16] = { 0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0 };
	return (m[i] + 0.5) / 16.0;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec3 n = abs(world_normal);
	bool cap = n.y > 0.5;
	float along = n.x > 0.5 ? world_pos.z : world_pos.x;               // horizontal coordinate on this face
	vec2 brick = vec2(along / 0.62, world_pos.y / 0.36);
	float row = floor(brick.y);
	brick.x += mod(row, 2.0) * 0.5;
	vec2 id = floor(brick);
	vec2 f = fract(brick);
	float joint = min(min(f.x, 1.0 - f.x) * 0.62, min(f.y, 1.0 - f.y) * 0.36);   // distance to the mortar, in units
	float block = smoothstep(0.012, 0.03, joint);

	vec2 uv = cap ? world_pos.xz : vec2(along, world_pos.y);
	float dirt = texture(grime, uv * 0.9).r;
	vec3 color = mix(block_dark.rgb, block_light.rgb, hash(id + (n.x > 0.5 ? vec2(31.0, 7.0) : vec2(0.0))));
	color *= mix(0.75, 1.1, dirt);
	color = mix(mortar.rgb, color, block);
	color *= mix(0.42, 1.0, smoothstep(0.0, 1.4, world_pos.y));        // darker, damp foot
	if (cap) color = mix(vec3(0.05, 0.045, 0.05), color * 0.35, 0.5);   // dark caps read as wall tops from above

	ALBEDO = color;
	ROUGHNESS = mix(1.0, 0.86, block);
	NORMAL_MAP = texture(normal_tex, uv * 0.5).rgb;
	NORMAL_MAP_DEPTH = 0.7;

	if (cut_enabled > 0.5) {
		vec3 seg = hero_position + vec3(0.0, 0.9, 0.0) - camera_position;
		float len = length(seg);
		vec3 dir = seg / len;
		float t = dot(world_pos - camera_position, dir);
		if (t > 0.0 && t < len - 0.5) {
			float d = length(world_pos - (camera_position + dir * t));
			float fade = 1.0 - smoothstep(cut_radius * 0.55, cut_radius, d);
			if (fade > bayer4(FRAGCOORD.xy)) discard;
		}
	}
}
