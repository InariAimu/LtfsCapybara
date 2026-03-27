add_executable(ltobox
        ltobox.c
        )

# pull in common dependencies
target_link_libraries(ltobox pico_stdlib)

# create map/bin/hex file etc.
pico_add_extra_outputs(ltobox)

# add url via pico_set_program_url
example_auto_set_url(ltobox)
