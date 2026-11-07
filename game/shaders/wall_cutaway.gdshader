// Stone wall with a see-through window: fragments between the camera and the hero dissolve with an ordered dither,
// so walls never hide the hero (works for every camera yaw and for the top-down view). Triplanar mapping in world
// space, so the instanced boxes need no UVs of their own. Written for the Compatibility renderer (GLSL ES 3).
shader_type spatial;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec4 tint : source_color = vec4(1.0);
uniform float uv_scale = 0.5;
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

void fragment() {
	vec3 n = abs(world_normal);
	vec2 uv = n.y > 0.5 ? world_pos.xz : (n.x > 0.5 ? world_pos.zy : world_pos.xy);
	uv *= uv_scale;
	vec3 color = texture(albedo_tex, uv).rgb * tint.rgb;
	float height = clamp(world_pos.y / wall_height, 0.0, 1.0);
	color *= mix(0.5, 1.0, height);                       // darker at the foot, grounds the wall
	if (n.y > 0.5) color *= 0.55;                          // wall tops read as a dark cap from above
	ALBEDO = color;
	ROUGHNESS = 0.93;
	NORMAL_MAP = texture(normal_tex, uv).rgb;
	NORMAL_MAP_DEPTH = 0.8;

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
