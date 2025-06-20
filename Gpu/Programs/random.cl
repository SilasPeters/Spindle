// TODO: might work better but triple the memory
// uint3 pgc3d(uint3* state) // From Jarzynski and Olano, 2020. “Hash Functions for GPU Rendering”. JCGT.
// {
//     uint3 v = *state;
//
//     v = v * 1664525u + 1013904223u;
//     v.x += v.y*v.z; v.y += v.z*v.x; v.z += v.x*v.y;
//     v ^= v >> 16u;
//     v.x += v.y*v.z; v.y += v.z*v.x; v.z += v.x*v.y;
//
//     *state = v;
//     return v;
// }

float RandomFloat(uint* state)
{
    // Marsaglia’s xorshift32 (from slideset)
    uint v = *state;

    v ^= v << 13;
    v ^= v >> 17;
    v ^= v << 5;

    *state = v;
    return v * 2.3283064365387e-10f;
}
