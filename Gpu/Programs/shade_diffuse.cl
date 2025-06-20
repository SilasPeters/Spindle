// This kernel calculates the extension ray and shadow ray, following
// an incoming ray on a simple diffuse material. Also calculated contributing illumance.

#include "structs.h"
#include "random.cl"
#include "utils.cl"

inline float3 hitpoint_from(const PathState p)
{
    return p.origin + p.direction * p.t;
}

inline float3 normal(const Sphere s, const float3 point)
{
    return (point - s.position) / s.radius;
}

__kernel void shade_diffuse(
    __global QueueStates *queue_states,
    __global uint *shade_diffuse_queue,
    __global uint *extend_ray_queue,
    __global uint *shadow_ray_queue,
    __global uint *random_states,
    __global PathState *path_states,
    __global Sphere *spheres,
    __global float3 *debug)
{
    // =====> Read context

    uint i = get_global_linear_id();
    uint path_state_index = shade_diffuse_queue[i];
    PathState path_state = path_states[path_state_index];
    Sphere sphere = spheres[path_state.object_id];

    // =====> Calculate bouncing ray and enqueue for extending

    float3 hitpoint = hitpoint_from(path_state);
    float3 normal_at_hitpoint = normal(sphere, hitpoint);
    float3 bounceDirection = CosineSampleHemisphere(normal_at_hitpoint, &random_states[path_state_index]);

    // Overwrite current ray with next ray to be extended
    path_states[path_state_index].origin = hitpoint;
    path_states[path_state_index].direction = bounceDirection;

    // Enqueue this path state for extension
    uint extend_ray_queue_length = atomic_inc(&queue_states->extend_ray_length); // TODO: assumes there always is space left on the queue
    extend_ray_queue[extend_ray_queue_length] = path_state_index;

    // =====> Enqueue shadow ray
    uint shadow_ray_queue_length = atomic_inc(&queue_states->shadow_ray_length); // TODO: assumes there always is space left on the queue
    shadow_ray_queue[shadow_ray_queue_length] = path_state_index;

    // =====> Dequeue processed jobs for this kernel

    atomic_dec(&queue_states->shade_diffuse_length);

    // Move unprocessed part of queue back to begin of buffer (always less than 1 local_size amount of items)
    uint local_id = get_local_id(0);
    shade_diffuse_queue[local_id] = shade_diffuse_queue[get_global_size(0) + local_id];
}
