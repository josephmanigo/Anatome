"""Regenerate the silicone teaching insert maps using the shared chest-wall authoring tools."""
from build_superficial_tissues import LEARNING, case_maps, write_maps


if __name__ == "__main__":
    write_maps(case_maps(), LEARNING)
