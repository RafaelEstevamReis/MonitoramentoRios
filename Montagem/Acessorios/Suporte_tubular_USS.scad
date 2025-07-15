$fn = 64;

tub_diameter = 23;
tub_length = 25;
tub_wall_thickness = 4;
sensor_diameter = 23;
wire_diameter = 3.5;
pin_diameter = 2;
// calculated
housing_diameter = 40;

opened = true;

difference() {
    union() {
        translate([0, tub_length-2, 0]) rotate([90,0,0]) 
            cylinder(h = tub_length+2, d = tub_diameter, center = true);
        cylinder(h = tub_diameter, d = housing_diameter, center = true, $fn=6);
    }
    // hollow
    translate([0, (tub_length + tub_diameter)/2, 0]) rotate([90,0,0]) 
        cylinder(h = tub_length + tub_diameter, d = tub_diameter - tub_wall_thickness, center = true, $fn=10);

    if(opened){
        // sensor
        cylinder(h = tub_diameter*1.1, d = sensor_diameter, center = true);
        // slit
        translate([0, -housing_diameter/2, 0]) 
            cube([wire_diameter, housing_diameter, tub_diameter*1.1], center=true);
    }else{
        // sensor
        translate([0, 0, -tub_diameter/2-1]) 
            cylinder(h = tub_diameter, d = sensor_diameter);
    }
    if(pin_diameter > 0){
      translate([0, tub_length-2, 0])rotate([0,90,0]) 
        #cylinder(h = tub_diameter*1.1, d = pin_diameter, center=true);
    }
}
