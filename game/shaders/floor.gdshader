// Crypt floor: irregular flagstones from two aligned cellular noises (cell value = tint per stone, distance =
// grout lines), fine grime on top, and contact darkening next to walls from a per-map proximity texture.
// Compatibility renderer (GLSL ES 3). All procedural; no external texture.
shader_type spatial;

uniform sampler2D stone_cells : filter_linear_mipmap, repeat_enable;   // CellValue
uniform sampler2D stone_edges : filter_linear_mipmap, repeat_enable;   // Distance2Sub
uniform sampler2D grime : filter_linear_mipmap, repeat_enable;
uniform sampler2D stone_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D proximity : filter_linear;                            // R: 0 at a wall .. 1 far from walls
uniform vec2 map_size = vec2(32.0, 16.0);
uniform vec4 stone_dark : source_color = vec4(0.16, 0.15, 0.15, 1.0);
uniform vec4 stone_light : source_color = vec4(0.31, 0.29, 0.27, 1.0);
uniform vec4 grout : source_color = vec4(0.07, 0.065, 0.06, 1.0);
uniform float uv_scale = 0.36;
uniform float contact_strength = 0.62;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = world_pos.xz * uv_scale;
	float cell = texture(stone_cells, uv).r;
	float edge = texture(stone_edges, uv).r;
	float dirt = texture(grime, uv * 2.7).r;
	float stone = smoothstep(0.018, 0.06, edge);                        // 0 in the grout, 1 on the stone
	vec3 color = mix(stone_dark.rgb, stone_light.rgb, cell);
	color *= mix(0.78, 1.08, dirt);
	color *= mix(0.86, 1.0, smoothstep(0.1, 0.45, edge));               // stones darken toward their rims
	color = mix(grout.rgb, color, stone);

	float near = texture(proximity, world_pos.xz / map_size).r;
	color *= mix(1.0 - contact_strength, 1.0, smoothstep(0.0, 1.0, near));

	ALBEDO = color;
	ROUGHNESS = mix(1.0, 0.82, stone * cell);
	NORMAL_MAP = texture(stone_normal, uv).rgb;
	NORMAL_MAP_DEPTH = 1.1;
}
