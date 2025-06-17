#include "structs.h"

// Accumulates radiance contributions and queues new ray generations.

uint convert_color(float3 rgb_floats)
{
    uint3 rgb = convert_uint3(rgb_floats * 255); // TODO Assumes range of 0..1
    return (rgb[0] << 16) | (rgb[1] << 8) | rgb[2];
}

__kernel void logic(
  __global QueueStates *queue_states,
  __global uint *new_ray_queue,
  __global uint *shade_diffuse_queue,
  __global uint *shade_reflective_queue,
  __global PathState *path_states,
  __global Material *materials,
  __global SceneInfo *sceneInfo,
  __global Sphere *spheres,
  __global Triangle *triangles,
  __global uint *image,
  __global float3 *debug)
{
    uint i = get_global_linear_id();

    // TODO: do not process rays still waiting in the queue of other kernels

    // =====> If this state has never been initialized, initialize it now by creating a new primary ray

    // TODO use a new variable for this. We have bytes unused anyway
    // TODO: We could also just not execute this stage as first, but this solution gives more flexibility
    if (path_states[i].t == 0 || path_states[i].t == -66) // No ray has been shot, or ray has been terminated
    {
        // Enqueue a new ray to be generated for this pixel
        uint queue_index = atomic_inc(&queue_states->new_ray_length);
        new_ray_queue[queue_index] = i; // Point to this ray

        // Communicate screen information (assumes this kernel is run in 2D over all pixels)
        uint x = get_global_id(0);
        uint y = get_global_id(1);
        path_states[i].origin = (float3)(x, y, 0); // Origin will be overwritten
        uint width = get_global_size(0); // assumes image size == screen size
        uint height = get_global_size(1);
        path_states[i].direction = (float3)(width, height, 0);

        return;
    }

    // =====> Process material evaluation
    // Using NEE, we accumulate the indirect light from the previous bounce.
    // If it was occluded, then accumulated_luminance has been set to 0 in the shadow ray kernel.

    // TODO outdated? It is (-1, -1, -1) when: direct light occluded or never sampled before
    if (any(path_states[i].latest_luminance_sample != (float3)(-1, -1, -1)))
    {
        path_states[i].accumulated_luminance *= path_states[i].latest_luminance_sample; // TODO overal fixed point arithmetics?
    }

    // =====> Process extension ray intersection, check for termination

    // TODO: implement russian roulette or max depth
    if (path_states[i].t < 0) // No intersection, terminate
    {
        // Taking the current determined sample, adjust the average
        uint sample_count = path_states[i].sample_count;
        path_states[i].averaged_samples =
            (path_states[i].averaged_samples * sample_count + path_states[i].accumulated_luminance)
            / (sample_count + 1);

        // Take the new average and progress to next sample
        image[i] = convert_color(path_states[i].averaged_samples);
        path_states[i].sample_count += 1;
        path_states[i].t = -66; // Execute order 66
        return;
    }

    // =====> Queue correct material kernel
    // If there is an intersection, queue the evaluation of its contribution towards indirect light.

    Sphere sphere = spheres[path_states[i].object_id];
    Material mat = materials[sphere.material];

    uint shade_diffuse_queue_index;
    uint shade_reflective_queue_index;

    switch (mat.type) {
        case mat_diffuse:
            shade_diffuse_queue_index = atomic_inc(&queue_states->shade_diffuse_length); // TODO: assumes there always is space
            shade_diffuse_queue[shade_diffuse_queue_index] = i; // Point to this path state
            break;

        case mat_reflective:
            shade_reflective_queue_index = atomic_inc(&queue_states->shade_reflective_length); // TODO: assumes there always is space
            shade_reflective_queue[shade_reflective_queue_index] = i; // Point to this path state
            break;
    }

    // We know the possible luminance in advance, store it already
    path_states[i].latest_luminance_sample = mat.color_times_albedo;

    // Set arguments for the shade phase
    path_states[i].material_id = sphere.material;
}