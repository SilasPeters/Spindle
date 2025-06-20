#include "structs.h"
#include "utils.cl"

__kernel void shadow(
  __global const SceneInfo *scene_info,
  __global QueueStates *queue_states,
  __global uint *shadow_ray_queue,
  __global PathState *path_states,
  __global const Sphere *spheres,
  __global const Triangle *triangles)
{
    uint i = get_global_linear_id();
    uint path_state_index = shadow_ray_queue[i];

    float3 point = path_states[path_state_index].origin;
    float3 lightPosition = (float3)(6, 6, -3); // TODO: hardcoded for now


    // =====> Test for occlusion

    PathState justWhatIfThisRayWentToTheLight; // Slight hack to not have to write another type and intersect method. Dirty i know
    justWhatIfThisRayWentToTheLight.origin = point;
    justWhatIfThisRayWentToTheLight.direction = fast_normalize(lightPosition - point);

    // For every sphere, test intersection // TODO: for now we only support spheres
    uint num_spheres = scene_info->num_spheres;
    for (int x = 0; x < num_spheres; x++)
    {
        float t = IntersectSphere(justWhatIfThisRayWentToTheLight, spheres[x]);
        if (t > 0) // Any intersection towards light
        {
            // Mark sample as invalid because hitpoint is occluded
            path_states[path_state_index].latest_luminance_sample *= .7f;
            break;
        }
    }

    // =====> Dequeue processed jobs for this kernel

    atomic_dec(&queue_states->shadow_ray_length);

    // Move unprocessed part of queue back to begin of buffer (always less than 1 local_size amount of items)
    uint local_id = get_local_id(0);
    shadow_ray_queue[local_id] = shadow_ray_queue[get_global_size(0) + local_id];
}